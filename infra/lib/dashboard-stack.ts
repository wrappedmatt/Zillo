import * as cdk from 'aws-cdk-lib';
import * as ecr from 'aws-cdk-lib/aws-ecr';
import * as iam from 'aws-cdk-lib/aws-iam';
import * as apprunner from 'aws-cdk-lib/aws-apprunner';
import * as secretsmanager from 'aws-cdk-lib/aws-secretsmanager';
import { Construct } from 'constructs';

interface DashboardStackProps extends cdk.StackProps {
  domainName: string;
  hostedZoneName: string;
  githubOwner: string;
  githubRepo: string;
  /** Set to true after first image is pushed to ECR */
  deployAppRunner?: boolean;
}

export class DashboardStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props: DashboardStackProps) {
    super(scope, id, props);

    // ECR Repository for Dashboard images - import existing if it exists
    const repository = ecr.Repository.fromRepositoryName(
      this,
      'DashboardRepository',
      'zillo-dashboard'
    );

    // GitHub OIDC Provider (check if already exists)
    const githubProvider = new iam.OpenIdConnectProvider(this, 'GitHubOIDCProvider', {
      url: 'https://token.actions.githubusercontent.com',
      clientIds: ['sts.amazonaws.com'],
      thumbprints: ['6938fd4d98bab03faadb97b34396831e3780aea1', '1c58a3a8518e8759bf075b76b750d4f2df264fcd'],
    });

    // IAM Role for GitHub Actions
    const githubActionsRole = new iam.Role(this, 'GitHubActionsRole', {
      roleName: 'Zillo-GitHubActions-Role',
      assumedBy: new iam.FederatedPrincipal(
        githubProvider.openIdConnectProviderArn,
        {
          StringEquals: {
            'token.actions.githubusercontent.com:aud': 'sts.amazonaws.com',
          },
          StringLike: {
            'token.actions.githubusercontent.com:sub': `repo:${props.githubOwner}/${props.githubRepo}:*`,
          },
        },
        'sts:AssumeRoleWithWebIdentity'
      ),
    });

    // Grant ECR permissions to GitHub Actions role
    repository.grantPullPush(githubActionsRole);

    // Also need GetAuthorizationToken for docker login
    githubActionsRole.addToPolicy(new iam.PolicyStatement({
      effect: iam.Effect.ALLOW,
      actions: ['ecr:GetAuthorizationToken'],
      resources: ['*'],
    }));

    // Create Secrets Manager secrets (empty - to be populated manually)
    const dashboardSecrets = new secretsmanager.Secret(this, 'DashboardSecrets', {
      secretName: 'Zillo/dashboard',
      description: 'Dashboard application secrets (Supabase, Stripe, etc.)',
      generateSecretString: {
        secretStringTemplate: JSON.stringify({
          'Supabase__Url': 'REPLACE_ME',
          'Supabase__Key': 'REPLACE_ME',
          'Stripe__SecretKey': 'REPLACE_ME',
          'Stripe__PublishableKey': 'REPLACE_ME',
          'Google__MapsApiKey': '',
          'Wallet__Apple__CertificatePassword': 'REPLACE_ME',
          'Wallet__WebServiceUrl': 'https://dashboard.zillo.app',
        }),
        generateStringKey: 'dummy', // Required but we'll delete it
      },
    });

    const appleP12Secret = new secretsmanager.Secret(this, 'AppleP12Secret', {
      secretName: 'Zillo/certs/apple-p12',
      description: 'Apple Wallet P12 certificate (base64 encoded)',
    });

    const googleKeySecret = new secretsmanager.Secret(this, 'GoogleKeySecret', {
      secretName: 'Zillo/certs/google-key',
      description: 'Google Wallet service account key JSON',
    });

    // App Runner Instance Role (for accessing Secrets Manager)
    const appRunnerInstanceRole = new iam.Role(this, 'AppRunnerInstanceRole', {
      roleName: 'Zillo-AppRunner-Instance-Role',
      assumedBy: new iam.ServicePrincipal('tasks.apprunner.amazonaws.com'),
    });

    // Grant secrets access
    dashboardSecrets.grantRead(appRunnerInstanceRole);
    appleP12Secret.grantRead(appRunnerInstanceRole);
    googleKeySecret.grantRead(appRunnerInstanceRole);

    // App Runner ECR Access Role
    const appRunnerAccessRole = new iam.Role(this, 'AppRunnerAccessRole', {
      roleName: 'Zillo-AppRunner-ECR-Access-Role',
      assumedBy: new iam.ServicePrincipal('build.apprunner.amazonaws.com'),
    });
    repository.grantPull(appRunnerAccessRole);

    // Outputs
    new cdk.CfnOutput(this, 'ECRRepositoryUri', {
      value: repository.repositoryUri,
      description: 'ECR Repository URI',
      exportName: 'Zillo-ECR-URI',
    });

    new cdk.CfnOutput(this, 'GitHubActionsRoleArn', {
      value: githubActionsRole.roleArn,
      description: 'IAM Role ARN for GitHub Actions (add to repo secrets as AWS_ROLE_ARN)',
      exportName: 'Zillo-GitHub-Role-ARN',
    });

    new cdk.CfnOutput(this, 'SecretsManagerArn', {
      value: dashboardSecrets.secretArn,
      description: 'Secrets Manager ARN - populate with actual values',
    });

    // App Runner Service - only deploy after first image is pushed
    if (props.deployAppRunner) {
      const appRunnerService = new apprunner.CfnService(this, 'DashboardService', {
        serviceName: 'Zillo-dashboard',
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
                {
                  name: 'APPLE_P12_SECRET_ARN',
                  value: appleP12Secret.secretArn,
                },
                {
                  name: 'GOOGLE_KEY_SECRET_ARN',
                  value: googleKeySecret.secretArn,
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
          cpu: '1024', // 1 vCPU
          memory: '2048', // 2 GB
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
1. Add GitHub secret: AWS_ROLE_ARN (see GitHubActionsRoleArn output)
2. Populate secrets in AWS Secrets Manager: ${dashboardSecrets.secretName}
3. Push first Docker image via GitHub Actions (create prod-release branch)
4. Set deployAppRunner: true in infra/bin/infra.ts and run cdk deploy again
`,
        description: 'Setup instructions',
      });
    }
  }
}
