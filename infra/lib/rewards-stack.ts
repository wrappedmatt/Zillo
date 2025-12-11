import * as cdk from 'aws-cdk-lib';
import * as ecr from 'aws-cdk-lib/aws-ecr';
import * as iam from 'aws-cdk-lib/aws-iam';
import * as apprunner from 'aws-cdk-lib/aws-apprunner';
import * as secretsmanager from 'aws-cdk-lib/aws-secretsmanager';
import { Construct } from 'constructs';

interface RewardsStackProps extends cdk.StackProps {
  domainName: string;
  /** Set to true after first image is pushed to ECR */
  deployAppRunner?: boolean;
  /** Reference to dashboard secrets (reuse same secrets) */
  dashboardSecretsArn?: string;
}

export class RewardsStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props: RewardsStackProps) {
    super(scope, id, props);

    // Create ECR Repository for Rewards images
    const repository = new ecr.Repository(this, 'RewardsRepository', {
      repositoryName: 'zillo-rewards',
      removalPolicy: cdk.RemovalPolicy.RETAIN,
      lifecycleRules: [
        {
          maxImageCount: 10,
          description: 'Keep only 10 images',
        },
      ],
    });

    // Note: GitHub Actions role permissions for ECR are managed in dashboard-stack.ts
    // The Zillo-GitHubActions-Role has permissions to push to all zillo-* ECR repos

    // Import dashboard secrets (reuse same credentials)
    const dashboardSecrets = props.dashboardSecretsArn
      ? secretsmanager.Secret.fromSecretCompleteArn(this, 'DashboardSecrets', props.dashboardSecretsArn)
      : secretsmanager.Secret.fromSecretNameV2(this, 'DashboardSecretsName', 'Zillo/dashboard');

    // App Runner ECR Access Role
    const appRunnerAccessRole = new iam.Role(this, 'AppRunnerAccessRole', {
      roleName: 'Zillo-Rewards-AppRunner-ECR-Access-Role',
      assumedBy: new iam.ServicePrincipal('build.apprunner.amazonaws.com'),
    });
    repository.grantPull(appRunnerAccessRole);

    // App Runner Instance Role (for accessing Secrets Manager)
    const appRunnerInstanceRole = new iam.Role(this, 'AppRunnerInstanceRole', {
      roleName: 'Zillo-Rewards-AppRunner-Instance-Role',
      assumedBy: new iam.ServicePrincipal('tasks.apprunner.amazonaws.com'),
    });

    // Grant secrets access
    dashboardSecrets.grantRead(appRunnerInstanceRole);

    // Outputs
    new cdk.CfnOutput(this, 'ECRRepositoryUri', {
      value: repository.repositoryUri,
      description: 'ECR Repository URI for Rewards',
      exportName: 'Zillo-Rewards-ECR-URI',
    });

    // App Runner Service - only deploy after first image is pushed
    if (props.deployAppRunner) {
      const appRunnerService = new apprunner.CfnService(this, 'RewardsService', {
        serviceName: 'Zillo-rewards',
        sourceConfiguration: {
          authenticationConfiguration: {
            accessRoleArn: appRunnerAccessRole.roleArn,
          },
          autoDeploymentsEnabled: true,
          imageRepository: {
            imageIdentifier: `${repository.repositoryUri}:latest`,
            imageRepositoryType: 'ECR',
            imageConfiguration: {
              port: '8080',
              runtimeEnvironmentSecrets: [
                {
                  name: 'APP_SECRETS_ARN',
                  value: dashboardSecrets.secretArn,
                },
              ],
              runtimeEnvironmentVariables: [
                {
                  name: 'ASPNETCORE_ENVIRONMENT',
                  value: 'Production',
                },
                {
                  name: 'AWS_REGION',
                  value: 'us-east-1',
                },
              ],
            },
          },
        },
        instanceConfiguration: {
          cpu: '512', // 0.5 vCPU (rewards is lightweight)
          memory: '1024', // 1 GB
          instanceRoleArn: appRunnerInstanceRole.roleArn,
        },
        healthCheckConfiguration: {
          protocol: 'HTTP',
          path: '/api/health',
          interval: 10,
          timeout: 5,
          healthyThreshold: 1,
          unhealthyThreshold: 5,
        },
      });

      appRunnerService.node.addDependency(appRunnerAccessRole);

      new cdk.CfnOutput(this, 'AppRunnerServiceUrl', {
        value: `https://${appRunnerService.attrServiceUrl}`,
        description: 'App Runner Service URL',
      });

      new cdk.CfnOutput(this, 'NextSteps', {
        value: `
1. Add custom domain in App Runner console: ${props.domainName}
2. Add Route 53 CNAME record for the custom domain
`,
        description: 'Setup instructions',
      });
    } else {
      new cdk.CfnOutput(this, 'NextSteps', {
        value: `
Phase 1 complete! Now:
1. Push first Docker image via GitHub Actions
2. Set deployAppRunner: true and run cdk deploy again
`,
        description: 'Setup instructions',
      });
    }
  }
}
