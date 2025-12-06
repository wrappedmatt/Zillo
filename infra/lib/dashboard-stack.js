"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || function (mod) {
    if (mod && mod.__esModule) return mod;
    var result = {};
    if (mod != null) for (var k in mod) if (k !== "default" && Object.prototype.hasOwnProperty.call(mod, k)) __createBinding(result, mod, k);
    __setModuleDefault(result, mod);
    return result;
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.DashboardStack = void 0;
const cdk = __importStar(require("aws-cdk-lib"));
const ecr = __importStar(require("aws-cdk-lib/aws-ecr"));
const iam = __importStar(require("aws-cdk-lib/aws-iam"));
const apprunner = __importStar(require("aws-cdk-lib/aws-apprunner"));
const secretsmanager = __importStar(require("aws-cdk-lib/aws-secretsmanager"));
const route53 = __importStar(require("aws-cdk-lib/aws-route53"));
class DashboardStack extends cdk.Stack {
    constructor(scope, id, props) {
        super(scope, id, props);
        // ECR Repository for Dashboard images
        const repository = new ecr.Repository(this, 'DashboardRepository', {
            repositoryName: 'Zillo-dashboard',
            removalPolicy: cdk.RemovalPolicy.RETAIN,
            imageScanOnPush: true,
            lifecycleRules: [
                {
                    maxImageCount: 10,
                    description: 'Keep only 10 images',
                },
            ],
        });
        // GitHub OIDC Provider (check if already exists)
        const githubProvider = new iam.OpenIdConnectProvider(this, 'GitHubOIDCProvider', {
            url: 'https://token.actions.githubusercontent.com',
            clientIds: ['sts.amazonaws.com'],
            thumbprints: ['6938fd4d98bab03faadb97b34396831e3780aea1', '1c58a3a8518e8759bf075b76b750d4f2df264fcd'],
        });
        // IAM Role for GitHub Actions
        const githubActionsRole = new iam.Role(this, 'GitHubActionsRole', {
            roleName: 'Zillo-GitHubActions-Role',
            assumedBy: new iam.FederatedPrincipal(githubProvider.openIdConnectProviderArn, {
                StringEquals: {
                    'token.actions.githubusercontent.com:aud': 'sts.amazonaws.com',
                },
                StringLike: {
                    'token.actions.githubusercontent.com:sub': `repo:${props.githubOwner}/${props.githubRepo}:*`,
                },
            }, 'sts:AssumeRoleWithWebIdentity'),
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
                    'Wallet__WebServiceUrl': 'https://app.Zilloup.com',
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
        // App Runner Service
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
        // Wait for ECR access role before creating service
        appRunnerService.node.addDependency(appRunnerAccessRole);
        // Look up the hosted zone
        const hostedZone = route53.HostedZone.fromLookup(this, 'HostedZone', {
            domainName: props.hostedZoneName,
        });
        // Custom domain association for App Runner (using CfnResource since L2 construct not available)
        const customDomain = new cdk.CfnResource(this, 'CustomDomain', {
            type: 'AWS::AppRunner::CustomDomainAssociation',
            properties: {
                DomainName: props.domainName,
                ServiceArn: appRunnerService.attrServiceArn,
                EnableWWWSubdomain: false,
            },
        });
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
        new cdk.CfnOutput(this, 'AppRunnerServiceUrl', {
            value: `https://${appRunnerService.attrServiceUrl}`,
            description: 'App Runner Service URL',
        });
        new cdk.CfnOutput(this, 'CustomDomainTarget', {
            value: customDomain.getAtt('DnsTarget').toString(),
            description: 'CNAME target for custom domain - add to Route 53',
        });
        new cdk.CfnOutput(this, 'SecretsManagerArn', {
            value: dashboardSecrets.secretArn,
            description: 'Secrets Manager ARN - populate with actual values',
        });
        // Output instructions
        new cdk.CfnOutput(this, 'NextSteps', {
            value: `
1. Add GitHub secret: AWS_ROLE_ARN = ${githubActionsRole.roleArn}
2. Populate secrets in AWS Console: ${dashboardSecrets.secretName}
3. Upload Apple P12 cert (base64): ${appleP12Secret.secretName}
4. Upload Google key.json: ${googleKeySecret.secretName}
5. Add Route 53 CNAME: ${props.domainName} -> (see CustomDomainTarget output)
`,
            description: 'Setup instructions',
        });
    }
}
exports.DashboardStack = DashboardStack;
//# sourceMappingURL=data:application/json;base64,eyJ2ZXJzaW9uIjozLCJmaWxlIjoiZGFzaGJvYXJkLXN0YWNrLmpzIiwic291cmNlUm9vdCI6IiIsInNvdXJjZXMiOlsiZGFzaGJvYXJkLXN0YWNrLnRzIl0sIm5hbWVzIjpbXSwibWFwcGluZ3MiOiI7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7O0FBQUEsaURBQW1DO0FBQ25DLHlEQUEyQztBQUMzQyx5REFBMkM7QUFDM0MscUVBQXVEO0FBQ3ZELCtFQUFpRTtBQUNqRSxpRUFBbUQ7QUFVbkQsTUFBYSxjQUFlLFNBQVEsR0FBRyxDQUFDLEtBQUs7SUFDM0MsWUFBWSxLQUFnQixFQUFFLEVBQVUsRUFBRSxLQUEwQjtRQUNsRSxLQUFLLENBQUMsS0FBSyxFQUFFLEVBQUUsRUFBRSxLQUFLLENBQUMsQ0FBQztRQUV4QixzQ0FBc0M7UUFDdEMsTUFBTSxVQUFVLEdBQUcsSUFBSSxHQUFHLENBQUMsVUFBVSxDQUFDLElBQUksRUFBRSxxQkFBcUIsRUFBRTtZQUNqRSxjQUFjLEVBQUUsdUJBQXVCO1lBQ3ZDLGFBQWEsRUFBRSxHQUFHLENBQUMsYUFBYSxDQUFDLE1BQU07WUFDdkMsZUFBZSxFQUFFLElBQUk7WUFDckIsY0FBYyxFQUFFO2dCQUNkO29CQUNFLGFBQWEsRUFBRSxFQUFFO29CQUNqQixXQUFXLEVBQUUscUJBQXFCO2lCQUNuQzthQUNGO1NBQ0YsQ0FBQyxDQUFDO1FBRUgsaURBQWlEO1FBQ2pELE1BQU0sY0FBYyxHQUFHLElBQUksR0FBRyxDQUFDLHFCQUFxQixDQUFDLElBQUksRUFBRSxvQkFBb0IsRUFBRTtZQUMvRSxHQUFHLEVBQUUsNkNBQTZDO1lBQ2xELFNBQVMsRUFBRSxDQUFDLG1CQUFtQixDQUFDO1lBQ2hDLFdBQVcsRUFBRSxDQUFDLDBDQUEwQyxFQUFFLDBDQUEwQyxDQUFDO1NBQ3RHLENBQUMsQ0FBQztRQUVILDhCQUE4QjtRQUM5QixNQUFNLGlCQUFpQixHQUFHLElBQUksR0FBRyxDQUFDLElBQUksQ0FBQyxJQUFJLEVBQUUsbUJBQW1CLEVBQUU7WUFDaEUsUUFBUSxFQUFFLGdD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UyxDQUFDLElBQUksRUFBRSxvQkFBb0IsRUFBRTtZQUM1QyxLQUFLLEVBQUUsWUFBWSxDQUFDLE1BQU0sQ0FBQyxXQUFXLENBQUMsQ0FBQyxRQUFRLEVBQUU7WUFDbEQsV0FBVyxFQUFFLGtEQUFrRDtTQUNoRSxDQUFDLENBQUM7UUFFSCxJQUFJLEdBQUcsQ0FBQyxTQUFTLENBQUMsSUFBSSxFQUFFLG1CQUFtQixFQUFFO1lBQzNDLEtBQUssRUFBRSxnQkFBZ0IsQ0FBQyxTQUFTO1lBQ2pDLFdBQVcsRUFBRSxtREFBbUQ7U0FDakUsQ0FBQyxDQUFDO1FBRUgsc0JBQXNCO1FBQ3RCLElBQUksR0FBRyxDQUFDLFNBQVMsQ0FBQyxJQUFJLEVBQUUsV0FBVyxFQUFFO1lBQ25DLEtBQUssRUFBRTt1Q0FDMEIsaUJBQWlCLENBQUMsT0FBTztzQ0FDMUIsZ0JBQWdCLENBQUMsVUFBVTtxQ0FDNUIsY0FBYyxDQUFDLFVBQVU7NkJBQ2pDLGVBQWUsQ0FBQyxVQUFVO3lCQUM5QixLQUFLLENBQUMsVUFBVTtDQUN4QztZQUNLLFdBQVcsRUFBRSxvQkFBb0I7U0FDbEMsQ0FBQyxDQUFDO0lBQ0wsQ0FBQztDQUNGO0FBbE5ELHdDQWtOQyIsInNvdXJjZXNDb250ZW50IjpbImltcG9ydCAqIGFzIGNkayBmcm9tICdhd3MtY2RrLWxpYic7XHJcbmltcG9ydCAqIGFzIGVjciBmcm9tICdhd3MtY2RrLWxpYi9hd3MtZWNyJztcclxuaW1wb3J0ICogYXMgaWFtIGZyb20gJ2F3cy1jZGstbGliL2F3cy1pYW0nO1xyXG5pbXBvcnQgKiBhcyBhcHBydW5uZXIgZnJvbSAnYXdzLWNkay1saWIvYXdzLWFwcHJ1bm5lcic7XHJcbmltcG9ydCAqIGFzIHNlY3JldHNtYW5hZ2VyIGZyb20gJ2F3cy1jZGstbGliL2F3cy1zZWNyZXRzbWFuYWdlcic7XHJcbmltcG9ydCAqIGFzIHJvdXRlNTMgZnJvbSAnYXdzLWNkay1saWIvYXdzLXJvdXRlNTMnO1xyXG5pbXBvcnQgeyBDb25zdHJ1Y3QgfSBmcm9tICdjb25zdHJ1Y3RzJztcclxuXHJcbmludGVyZmFjZSBEYXNoYm9hcmRTdGFja1Byb3BzIGV4dGVuZHMgY2RrLlN0YWNrUHJvcHMge1xyXG4gIGRvbWFpbk5hbWU6IHN0cmluZztcclxuICBob3N0ZWRab25lTmFtZTogc3RyaW5nO1xyXG4gIGdpdGh1Yk93bmVyOiBzdHJpbmc7XHJcbiAgZ2l0aHViUmVwbzogc3RyaW5nO1xyXG59XHJcblxyXG5leHBvcnQgY2xhc3MgRGFzaGJvYXJkU3RhY2sgZXh0ZW5kcyBjZGsuU3RhY2sge1xyXG4gIGNvbnN0cnVjdG9yKHNjb3BlOiBDb25zdHJ1Y3QsIGlkOiBzdHJpbmcsIHByb3BzOiBEYXNoYm9hcmRTdGFja1Byb3BzKSB7XHJcbiAgICBzdXBlcihzY29wZSwgaWQsIHByb3BzKTtcclxuXHJcbiAgICAvLyBFQ1IgUmVwb3NpdG9yeSBmb3IgRGFzaGJvYXJkIGltYWdlc1xyXG4gICAgY29uc3QgcmVwb3NpdG9yeSA9IG5ldyBlY3IuUmVwb3NpdG9yeSh0aGlzLCAnRGFzaGJvYXJkUmVwb3NpdG9yeScsIHtcclxuICAgICAgcmVwb3NpdG9yeU5hbWU6ICdsZW1vbmFkZWFwcC1kYXNoYm9hcmQnLFxyXG4gICAgICByZW1vdmFsUG9saWN5OiBjZGsuUmVtb3ZhbFBvbGljeS5SRVRBSU4sXHJcbiAgICAgIGltYWdlU2Nhbk9uUHVzaDogdHJ1ZSxcclxuICAgICAgbGlmZWN5Y2xlUnVsZXM6IFtcclxuICAgICAgICB7XHJcbiAgICAgICAgICBtYXhJbWFnZUNvdW50OiAxMCxcclxuICAgICAgICAgIGRlc2NyaXB0aW9uOiAnS2VlcCBvbmx5IDEwIGltYWdlcycsXHJcbiAgICAgICAgfSxcclxuICAgICAgXSxcclxuICAgIH0pO1xyXG5cclxuICAgIC8vIEdpdEh1YiBPSURDIFByb3ZpZGVyIChjaGVjayBpZiBhbHJlYWR5IGV4aXN0cylcclxuICAgIGNvbnN0IGdpdGh1YlByb3ZpZGVyID0gbmV3IGlhbS5PcGVuSWRDb25uZWN0UHJvdmlkZXIodGhpcywgJ0dpdEh1Yk9JRENQcm92aWRlcicsIHtcclxuICAgICAgdXJsOiAnaHR0cHM6Ly90b2tlbi5hY3Rpb25zLmdpdGh1YnVzZXJjb250ZW50LmNvbScsXHJcbiAgICAgIGNsaWVudElkczogWydzdHMuYW1hem9uYXdzLmNvbSddLFxyXG4gICAgICB0aHVtYnByaW50czogWyc2OTM4ZmQ0ZDk4YmFiMDNmYWFkYjk3YjM0Mzk2ODMxZTM3ODBhZWExJywgJzFjNThhM2E4NTE4ZTg3NTliZjA3NWI3NmI3NTBkNGYyZGYyNjRmY2QnXSxcclxuICAgIH0pO1xyXG5cclxuICAgIC8vIElBTSBSb2xlIGZvciBHaXRIdWIgQWN0aW9uc1xyXG4gICAgY29uc3QgZ2l0aHViQWN0aW9uc1JvbGUgPSBuZXcgaWFtLlJvbGUodGhpcywgJ0dpdEh1YkFjdGlvbnNSb2xlJywge1xyXG4gICAgICByb2xlTmFtZTogJ0xlbW9uYWRlQXBwLUdpdEh1YkFjdGlvbnMtUm9sZScsXHJcbiAgICAgIGFzc3VtZWRCeTogbmV3IGlhbS5GZWRlcmF0ZWRQcmluY2lwYWwoXHJcbiAgICAgICAgZ2l0aHViUHJvdmlkZXIub3BlbklkQ29ubmVjdFByb3ZpZGVyQXJuLFxyXG4gICAgICAgIHtcclxuICAgICAgICAgIFN0cmluZ0VxdWFsczoge1xyXG4gICAgICAgICAgICAndG9rZW4uYWN0aW9ucy5naXRodWJ1c2VyY29udGVudC5jb206YXVkJzogJ3N0cy5hbWF6b25hd3MuY29tJyxcclxuICAgICAgICAgIH0sXHJcbiAgICAgICAgICBTdHJpbmdMaWtlOiB7XHJcbiAgICAgICAgICAgICd0b2tlbi5hY3Rpb25zLmdpdGh1YnVzZXJjb250ZW50LmNvbTpzdWInOiBgcmVwbzoke3Byb3BzLmdpdGh1Yk93bmVyfS8ke3Byb3BzLmdpdGh1YlJlcG99OipgLFxyXG4gICAgICAgICAgfSxcclxuICAgICAgICB9LFxyXG4gICAgICAgICdzdHM6QXNzdW1lUm9sZVdpdGhXZWJJZGVudGl0eSdcclxuICAgICAgKSxcclxuICAgIH0pO1xyXG5cclxuICAgIC8vIEdyYW50IEVDUiBwZXJtaXNzaW9ucyB0byBHaXRIdWIgQWN0aW9ucyByb2xlXHJcbiAgICByZXBvc2l0b3J5LmdyYW50UHVsbFB1c2goZ2l0aHViQWN0aW9uc1JvbGUpO1xyXG5cclxuICAgIC8vIEFsc28gbmVlZCBHZXRBdXRob3JpemF0aW9uVG9rZW4gZm9yIGRvY2tlciBsb2dpblxyXG4gICAgZ2l0aHViQWN0aW9uc1JvbGUuYWRkVG9Qb2xpY3kobmV3IGlhbS5Qb2xpY3lTdGF0ZW1lbnQoe1xyXG4gICAgICBlZmZlY3Q6IGlhbS5FZmZlY3QuQUxMT1csXHJcbiAgICAgIGFjdGlvbnM6IFsnZWNyOkdldEF1dGhvcml6YXRpb25Ub2tlbiddLFxyXG4gICAgICByZXNvdXJjZXM6IFsnKiddLFxyXG4gICAgfSkpO1xyXG5cclxuICAgIC8vIENyZWF0ZSBTZWNyZXRzIE1hbmFnZXIgc2VjcmV0cyAoZW1wdHkgLSB0byBiZSBwb3B1bGF0ZWQgbWFudWFsbHkpXHJcbiAgICBjb25zdCBkYXNoYm9hcmRTZWNyZXRzID0gbmV3IHNlY3JldHNtYW5hZ2VyLlNlY3JldCh0aGlzLCAnRGFzaGJvYXJkU2VjcmV0cycsIHtcclxuICAgICAgc2VjcmV0TmFtZTogJ2xlbW9uYWRlYXBwL2Rhc2hib2FyZCcsXHJcbiAgICAgIGRlc2NyaXB0aW9uOiAnRGFzaGJvYXJkIGFwcGxpY2F0aW9uIHNlY3JldHMgKFN1cGFiYXNlLCBTdHJpcGUsIGV0Yy4pJyxcclxuICAgICAgZ2VuZXJhdGVTZWNyZXRTdHJpbmc6IHtcclxuICAgICAgICBzZWNyZXRTdHJpbmdUZW1wbGF0ZTogSlNPTi5zdHJpbmdpZnkoe1xyXG4gICAgICAgICAgJ1N1cGFiYXNlX19VcmwnOiAnUkVQTEFDRV9NRScsXHJcbiAgICAgICAgICAnU3VwYWJhc2VfX0tleSc6ICdSRVBMQUNFX01FJyxcclxuICAgICAgICAgICdTdHJpcGVfX1NlY3JldEtleSc6ICdSRVBMQUNFX01FJyxcclxuICAgICAgICAgICdTdHJpcGVfX1B1Ymxpc2hhYmxlS2V5JzogJ1JFUExBQ0VfTUUnLFxyXG4gICAgICAgICAgJ0dvb2dsZV9fTWFwc0FwaUtleSc6ICcnLFxyXG4gICAgICAgICAgJ1dhbGxldF9fQXBwbGVfX0NlcnRpZmljYXRlUGFzc3dvcmQnOiAnUkVQTEFDRV9NRScsXHJcbiAgICAgICAgICAnV2FsbGV0X19XZWJTZXJ2aWNlVXJsJzogJ2h0dHBzOi8vYXBwLmxlbW9uYWRldXAuY29tJyxcclxuICAgICAgICB9KSxcclxuICAgICAgICBnZW5lcmF0ZVN0cmluZ0tleTogJ2R1bW15JywgLy8gUmVxdWlyZWQgYnV0IHdlJ2xsIGRlbGV0ZSBpdFxyXG4gICAgICB9LFxyXG4gICAgfSk7XHJcblxyXG4gICAgY29uc3QgYXBwbGVQMTJTZWNyZXQgPSBuZXcgc2VjcmV0c21hbmFnZXIuU2VjcmV0KHRoaXMsICdBcHBsZVAxMlNlY3JldCcsIHtcclxuICAgICAgc2VjcmV0TmFtZTogJ2xlbW9uYWRlYXBwL2NlcnRzL2FwcGxlLXAxMicsXHJcbiAgICAgIGRlc2NyaXB0aW9uOiAnQXBwbGUgV2FsbGV0IFAxMiBjZXJ0aWZpY2F0ZSAoYmFzZTY0IGVuY29kZWQpJyxcclxuICAgIH0pO1xyXG5cclxuICAgIGNvbnN0IGdvb2dsZUtleVNlY3JldCA9IG5ldyBzZWNyZXRzbWFuYWdlci5TZWNyZXQodGhpcywgJ0dvb2dsZUtleVNlY3JldCcsIHtcclxuICAgICAgc2VjcmV0TmFtZTogJ2xlbW9uYWRlYXBwL2NlcnRzL2dvb2dsZS1rZXknLFxyXG4gICAgICBkZXNjcmlwdGlvbjogJ0dvb2dsZSBXYWxsZXQgc2VydmljZSBhY2NvdW50IGtleSBKU09OJyxcclxuICAgIH0pO1xyXG5cclxuICAgIC8vIEFwcCBSdW5uZXIgSW5zdGFuY2UgUm9sZSAoZm9yIGFjY2Vzc2luZyBTZWNyZXRzIE1hbmFnZXIpXHJcbiAgICBjb25zdCBhcHBSdW5uZXJJbnN0YW5jZVJvbGUgPSBuZXcgaWFtLlJvbGUodGhpcywgJ0FwcFJ1bm5lckluc3RhbmNlUm9sZScsIHtcclxuICAgICAgcm9sZU5hbWU6ICdMZW1vbmFkZUFwcC1BcHBSdW5uZXItSW5zdGFuY2UtUm9sZScsXHJcbiAgICAgIGFzc3VtZWRCeTogbmV3IGlhbS5TZXJ2aWNlUHJpbmNpcGFsKCd0YXNrcy5hcHBydW5uZXIuYW1hem9uYXdzLmNvbScpLFxyXG4gICAgfSk7XHJcblxyXG4gICAgLy8gR3JhbnQgc2VjcmV0cyBhY2Nlc3NcclxuICAgIGRhc2hib2FyZFNlY3JldHMuZ3JhbnRSZWFkKGFwcFJ1bm5lckluc3RhbmNlUm9sZSk7XHJcbiAgICBhcHBsZVAxMlNlY3JldC5ncmFudFJlYWQoYXBwUnVubmVySW5zdGFuY2VSb2xlKTtcclxuICAgIGdvb2dsZUtleVNlY3JldC5ncmFudFJlYWQoYXBwUnVubmVySW5zdGFuY2VSb2xlKTtcclxuXHJcbiAgICAvLyBBcHAgUnVubmVyIEVDUiBBY2Nlc3MgUm9sZVxyXG4gICAgY29uc3QgYXBwUnVubmVyQWNjZXNzUm9sZSA9IG5ldyBpYW0uUm9sZSh0aGlzLCAnQXBwUnVubmVyQWNjZXNzUm9sZScsIHtcclxuICAgICAgcm9sZU5hbWU6ICdMZW1vbmFkZUFwcC1BcHBSdW5uZXItRUNSLUFjY2Vzcy1Sb2xlJyxcclxuICAgICAgYXNzdW1lZEJ5OiBuZXcgaWFtLlNlcnZpY2VQcmluY2lwYWwoJ2J1aWxkLmFwcHJ1bm5lci5hbWF6b25hd3MuY29tJyksXHJcbiAgICB9KTtcclxuICAgIHJlcG9zaXRvcnkuZ3JhbnRQdWxsKGFwcFJ1bm5lckFjY2Vzc1JvbGUpO1xyXG5cclxuICAgIC8vIEFwcCBSdW5uZXIgU2VydmljZVxyXG4gICAgY29uc3QgYXBwUnVubmVyU2VydmljZSA9IG5ldyBhcHBydW5uZXIuQ2ZuU2VydmljZSh0aGlzLCAnRGFzaGJvYXJkU2VydmljZScsIHtcclxuICAgICAgc2VydmljZU5hbWU6ICdsZW1vbmFkZWFwcC1kYXNoYm9hcmQnLFxyXG4gICAgICBzb3VyY2VDb25maWd1cmF0aW9uOiB7XHJcbiAgICAgICAgYXV0aGVudGljYXRpb25Db25maWd1cmF0aW9uOiB7XHJcbiAgICAgICAgICBhY2Nlc3NSb2xlQXJuOiBhcHBSdW5uZXJBY2Nlc3NSb2xlLnJvbGVBcm4sXHJcbiAgICAgICAgfSxcclxuICAgICAgICBhdXRvRGVwbG95bWVudHNFbmFibGVkOiB0cnVlLFxyXG4gICAgICAgIGltYWdlUmVwb3NpdG9yeToge1xyXG4gICAgICAgICAgaW1hZ2VJZGVudGlmaWVyOiBgJHtyZXBvc2l0b3J5LnJlcG9zaXRvcnlVcml9OmxhdGVzdGAsXHJcbiAgICAgICAgICBpbWFnZVJlcG9zaXRvcnlUeXBlOiAnRUNSJyxcclxuICAgICAgICAgIGltYWdlQ29uZmlndXJhdGlvbjoge1xyXG4gICAgICAgICAgICBwb3J0OiAnODA4MCcsXHJcbiAgICAgICAgICAgIHJ1bnRpbWVFbnZpcm9ubWVudFNlY3JldHM6IFtcclxuICAgICAgICAgICAgICB7XHJcbiAgICAgICAgICAgICAgICBuYW1lOiAnQVBQX1NFQ1JFVFNfQVJOJyxcclxuICAgICAgICAgICAgICAgIHZhbHVlOiBkYXNoYm9hcmRTZWNyZXRzLnNlY3JldEFybixcclxuICAgICAgICAgICAgICB9LFxyXG4gICAgICAgICAgICAgIHtcclxuICAgICAgICAgICAgICAgIG5hbWU6ICdBUFBMRV9QMTJfU0VDUkVUX0FSTicsXHJcbiAgICAgICAgICAgICAgICB2YWx1ZTogYXBwbGVQMTJTZWNyZXQuc2VjcmV0QXJuLFxyXG4gICAgICAgICAgICAgIH0sXHJcbiAgICAgICAgICAgICAge1xyXG4gICAgICAgICAgICAgICAgbmFtZTogJ0dPT0dMRV9LRVlfU0VDUkVUX0FSTicsXHJcbiAgICAgICAgICAgICAgICB2YWx1ZTogZ29vZ2xlS2V5U2VjcmV0LnNlY3JldEFybixcclxuICAgICAgICAgICAgICB9LFxyXG4gICAgICAgICAgICBdLFxyXG4gICAgICAgICAgICBydW50aW1lRW52aXJvbm1lbnRWYXJpYWJsZXM6IFtcclxuICAgICAgICAgICAgICB7XHJcbiAgICAgICAgICAgICAgICBuYW1lOiAnQVNQTkVUQ09SRV9FTlZJUk9OTUVOVCcsXHJcbiAgICAgICAgICAgICAgICB2YWx1ZTogJ1Byb2R1Y3Rpb24nLFxyXG4gICAgICAgICAgICAgIH0sXHJcbiAgICAgICAgICAgICAge1xyXG4gICAgICAgICAgICAgICAgbmFtZTogJ0FXU19SRUdJT04nLFxyXG4gICAgICAgICAgICAgICAgdmFsdWU6ICd1cy1lYXN0LTEnLFxyXG4gICAgICAgICAgICAgIH0sXHJcbiAgICAgICAgICAgIF0sXHJcbiAgICAgICAgICB9LFxyXG4gICAgICAgIH0sXHJcbiAgICAgIH0sXHJcbiAgICAgIGluc3RhbmNlQ29uZmlndXJhdGlvbjoge1xyXG4gICAgICAgIGNwdTogJzEwMjQnLCAvLyAxIHZDUFVcclxuICAgICAgICBtZW1vcnk6ICcyMDQ4JywgLy8gMiBHQlxyXG4gICAgICAgIGluc3RhbmNlUm9sZUFybjogYXBwUnVubmVySW5zdGFuY2VSb2xlLnJvbGVBcm4sXHJcbiAgICAgIH0sXHJcbiAgICAgIGhlYWx0aENoZWNrQ29uZmlndXJhdGlvbjoge1xyXG4gICAgICAgIHByb3RvY29sOiAnSFRUUCcsXHJcbiAgICAgICAgcGF0aDogJy9hcGkvaGVhbHRoJyxcclxuICAgICAgICBpbnRlcnZhbDogMTAsXHJcbiAgICAgICAgdGltZW91dDogNSxcclxuICAgICAgICBoZWFsdGh5VGhyZXNob2xkOiAxLFxyXG4gICAgICAgIHVuaGVhbHRoeVRocmVzaG9sZDogNSxcclxuICAgICAgfSxcclxuICAgIH0pO1xyXG5cclxuICAgIC8vIFdhaXQgZm9yIEVDUiBhY2Nlc3Mgcm9sZSBiZWZvcmUgY3JlYXRpbmcgc2VydmljZVxyXG4gICAgYXBwUnVubmVyU2VydmljZS5ub2RlLmFkZERlcGVuZGVuY3koYXBwUnVubmVyQWNjZXNzUm9sZSk7XHJcblxyXG4gICAgLy8gTG9vayB1cCB0aGUgaG9zdGVkIHpvbmVcclxuICAgIGNvbnN0IGhvc3RlZFpvbmUgPSByb3V0ZTUzLkhvc3RlZFpvbmUuZnJvbUxvb2t1cCh0aGlzLCAnSG9zdGVkWm9uZScsIHtcclxuICAgICAgZG9tYWluTmFtZTogcHJvcHMuaG9zdGVkWm9uZU5hbWUsXHJcbiAgICB9KTtcclxuXHJcbiAgICAvLyBDdXN0b20gZG9tYWluIGFzc29jaWF0aW9uIGZvciBBcHAgUnVubmVyICh1c2luZyBDZm5SZXNvdXJjZSBzaW5jZSBMMiBjb25zdHJ1Y3Qgbm90IGF2YWlsYWJsZSlcclxuICAgIGNvbnN0IGN1c3RvbURvbWFpbiA9IG5ldyBjZGsuQ2ZuUmVzb3VyY2UodGhpcywgJ0N1c3RvbURvbWFpbicsIHtcclxuICAgICAgdHlwZTogJ0FXUzo6QXBwUnVubmVyOjpDdXN0b21Eb21haW5Bc3NvY2lhdGlvbicsXHJcbiAgICAgIHByb3BlcnRpZXM6IHtcclxuICAgICAgICBEb21haW5OYW1lOiBwcm9wcy5kb21haW5OYW1lLFxyXG4gICAgICAgIFNlcnZpY2VBcm46IGFwcFJ1bm5lclNlcnZpY2UuYXR0clNlcnZpY2VBcm4sXHJcbiAgICAgICAgRW5hYmxlV1dXU3ViZG9tYWluOiBmYWxzZSxcclxuICAgICAgfSxcclxuICAgIH0pO1xyXG5cclxuICAgIC8vIE91dHB1dHNcclxuICAgIG5ldyBjZGsuQ2ZuT3V0cHV0KHRoaXMsICdFQ1JSZXBvc2l0b3J5VXJpJywge1xyXG4gICAgICB2YWx1ZTogcmVwb3NpdG9yeS5yZXBvc2l0b3J5VXJpLFxyXG4gICAgICBkZXNjcmlwdGlvbjogJ0VDUiBSZXBvc2l0b3J5IFVSSScsXHJcbiAgICAgIGV4cG9ydE5hbWU6ICdMZW1vbmFkZUFwcC1FQ1ItVVJJJyxcclxuICAgIH0pO1xyXG5cclxuICAgIG5ldyBjZGsuQ2ZuT3V0cHV0KHRoaXMsICdHaXRIdWJBY3Rpb25zUm9sZUFybicsIHtcclxuICAgICAgdmFsdWU6IGdpdGh1YkFjdGlvbnNSb2xlLnJvbGVBcm4sXHJcbiAgICAgIGRlc2NyaXB0aW9uOiAnSUFNIFJvbGUgQVJOIGZvciBHaXRIdWIgQWN0aW9ucyAoYWRkIHRvIHJlcG8gc2VjcmV0cyBhcyBBV1NfUk9MRV9BUk4pJyxcclxuICAgICAgZXhwb3J0TmFtZTogJ0xlbW9uYWRlQXBwLUdpdEh1Yi1Sb2xlLUFSTicsXHJcbiAgICB9KTtcclxuXHJcbiAgICBuZXcgY2RrLkNmbk91dHB1dCh0aGlzLCAnQXBwUnVubmVyU2VydmljZVVybCcsIHtcclxuICAgICAgdmFsdWU6IGBodHRwczovLyR7YXBwUnVubmVyU2VydmljZS5hdHRyU2VydmljZVVybH1gLFxyXG4gICAgICBkZXNjcmlwdGlvbjogJ0FwcCBSdW5uZXIgU2VydmljZSBVUkwnLFxyXG4gICAgfSk7XHJcblxyXG4gICAgbmV3IGNkay5DZm5PdXRwdXQodGhpcywgJ0N1c3RvbURvbWFpblRhcmdldCcsIHtcclxuICAgICAgdmFsdWU6IGN1c3RvbURvbWFpbi5nZXRBdHQoJ0Ruc1RhcmdldCcpLnRvU3RyaW5nKCksXHJcbiAgICAgIGRlc2NyaXB0aW9uOiAnQ05BTUUgdGFyZ2V0IGZvciBjdXN0b20gZG9tYWluIC0gYWRkIHRvIFJvdXRlIDUzJyxcclxuICAgIH0pO1xyXG5cclxuICAgIG5ldyBjZGsuQ2ZuT3V0cHV0KHRoaXMsICdTZWNyZXRzTWFuYWdlckFybicsIHtcclxuICAgICAgdmFsdWU6IGRhc2hib2FyZFNlY3JldHMuc2VjcmV0QXJuLFxyXG4gICAgICBkZXNjcmlwdGlvbjogJ1NlY3JldHMgTWFuYWdlciBBUk4gLSBwb3B1bGF0ZSB3aXRoIGFjdHVhbCB2YWx1ZXMnLFxyXG4gICAgfSk7XHJcblxyXG4gICAgLy8gT3V0cHV0IGluc3RydWN0aW9uc1xyXG4gICAgbmV3IGNkay5DZm5PdXRwdXQodGhpcywgJ05leHRTdGVwcycsIHtcclxuICAgICAgdmFsdWU6IGBcclxuMS4gQWRkIEdpdEh1YiBzZWNyZXQ6IEFXU19ST0xFX0FSTiA9ICR7Z2l0aHViQWN0aW9uc1JvbGUucm9sZUFybn1cclxuMi4gUG9wdWxhdGUgc2VjcmV0cyBpbiBBV1MgQ29uc29sZTogJHtkYXNoYm9hcmRTZWNyZXRzLnNlY3JldE5hbWV9XHJcbjMuIFVwbG9hZCBBcHBsZSBQMTIgY2VydCAoYmFzZTY0KTogJHthcHBsZVAxMlNlY3JldC5zZWNyZXROYW1lfVxyXG40LiBVcGxvYWQgR29vZ2xlIGtleS5qc29uOiAke2dvb2dsZUtleVNlY3JldC5zZWNyZXROYW1lfVxyXG41LiBBZGQgUm91dGUgNTMgQ05BTUU6ICR7cHJvcHMuZG9tYWluTmFtZX0gLT4gKHNlZSBDdXN0b21Eb21haW5UYXJnZXQgb3V0cHV0KVxyXG5gLFxyXG4gICAgICBkZXNjcmlwdGlvbjogJ1NldHVwIGluc3RydWN0aW9ucycsXHJcbiAgICB9KTtcclxuICB9XHJcbn1cclxuIl19