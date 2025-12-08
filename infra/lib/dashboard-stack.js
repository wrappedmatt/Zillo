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
class DashboardStack extends cdk.Stack {
    constructor(scope, id, props) {
        super(scope, id, props);
        // ECR Repository for Dashboard images - import existing if it exists
        const repository = ecr.Repository.fromRepositoryName(this, 'DashboardRepository', 'zillo-dashboard');
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
        }
        else {
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
exports.DashboardStack = DashboardStack;
//# sourceMappingURL=data:application/json;base64,eyJ2ZXJzaW9uIjozLCJmaWxlIjoiZGFzaGJvYXJkLXN0YWNrLmpzIiwic291cmNlUm9vdCI6IiIsInNvdXJjZXMiOlsiZGFzaGJvYXJkLXN0YWNrLnRzIl0sIm5hbWVzIjpbXSwibWFwcGluZ3MiOiI7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7O0FBQUEsaURBQW1DO0FBQ25DLHlEQUEyQztBQUMzQyx5REFBMkM7QUFDM0MscUVBQXVEO0FBQ3ZELCtFQUFpRTtBQVlqRSxNQUFhLGNBQWUsU0FBUSxHQUFHLENBQUMsS0FBSztJQUMzQyxZQUFZLEtBQWdCLEVBQUUsRUFBVSxFQUFFLEtBQTBCO1FBQ2xFLEtBQUssQ0FBQyxLQUFLLEVBQUUsRUFBRSxFQUFFLEtBQUssQ0FBQyxDQUFDO1FBRXhCLHFFQUFxRTtRQUNyRSxNQUFNLFVBQVUsR0FBRyxHQUFHLENBQUMsVUFBVSxDQUFDLGtCQUFrQixDQUNsRCxJQUFJLEVBQ0oscUJBQXFCLEVBQ3JCLGlCQUFpQixDQUNsQixDQUFDO1FBRUYsaURBQWlEO1FBQ2pELE1BQU0sY0FBYyxHQUFHLElBQUksR0FBRyxDQUFDLHFCQUFxQixDQUFDLElBQUksRUFBRSxvQkFBb0IsRUFBRTtZQUMvRSxHQUFHLEVBQUUsNkNBQTZDO1lBQ2xELFNBQVMsRUFBRSxDQUFDLG1CQUFtQixDQUFDO1lBQ2hDLFdBQVcsRUFBRSxDQUFDLDBDQUEwQyxFQUFFLDBDQUEwQyxDQUFDO1NBQ3RHLENBQUMsQ0FBQztRQUVILDhCQUE4QjtRQUM5QixNQUFNLGlCQUFpQixHQUFHLElBQUksR0FBRyxDQUFDLElBQUksQ0FBQyxJQUFJLEVBQUUsbUJBQW1CLEVBQUU7WUFDaEUsUUFBUSxFQUFFLDBCQUEwQjtZQUNwQyxTQUFTLEVBQUUsSUFBSSxHQUFHLENBQUMsa0JBQWtCLENBQ25DLGNBQWMsQ0FBQyx3QkFBd0IsRUFDdkM7Z0JBQ0UsWUFBWSxFQUFFO29CQUNaLHlDQUF5QyxFQUFFLG1CQUFtQjtpQkFDL0Q7Z0JBQ0QsVUFBVSxFQUFFO29CQUNWLHlDQUF5QyxFQUFFLFFBQVEsS0FBSyxD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bmltcG9ydCAqIGFzIGVjciBmcm9tICdhd3MtY2RrLWxpYi9hd3MtZWNyJztcclxuaW1wb3J0ICogYXMgaWFtIGZyb20gJ2F3cy1jZGstbGliL2F3cy1pYW0nO1xyXG5pbXBvcnQgKiBhcyBhcHBydW5uZXIgZnJvbSAnYXdzLWNkay1saWIvYXdzLWFwcHJ1bm5lcic7XHJcbmltcG9ydCAqIGFzIHNlY3JldHNtYW5hZ2VyIGZyb20gJ2F3cy1jZGstbGliL2F3cy1zZWNyZXRzbWFuYWdlcic7XHJcbmltcG9ydCB7IENvbnN0cnVjdCB9IGZyb20gJ2NvbnN0cnVjdHMnO1xyXG5cclxuaW50ZXJmYWNlIERhc2hib2FyZFN0YWNrUHJvcHMgZXh0ZW5kcyBjZGsuU3RhY2tQcm9wcyB7XHJcbiAgZG9tYWluTmFtZTogc3RyaW5nO1xyXG4gIGhvc3RlZFpvbmVOYW1lOiBzdHJpbmc7XHJcbiAgZ2l0aHViT3duZXI6IHN0cmluZztcclxuICBnaXRodWJSZXBvOiBzdHJpbmc7XHJcbiAgLyoqIFNldCB0byB0cnVlIGFmdGVyIGZpcnN0IGltYWdlIGlzIHB1c2hlZCB0byBFQ1IgKi9cclxuICBkZXBsb3lBcHBSdW5uZXI/OiBib29sZWFuO1xyXG59XHJcblxyXG5leHBvcnQgY2xhc3MgRGFzaGJvYXJkU3RhY2sgZXh0ZW5kcyBjZGsuU3RhY2sge1xyXG4gIGNvbnN0cnVjdG9yKHNjb3BlOiBDb25zdHJ1Y3QsIGlkOiBzdHJpbmcsIHByb3BzOiBEYXNoYm9hcmRTdGFja1Byb3BzKSB7XHJcbiAgICBzdXBlcihzY29wZSwgaWQsIHByb3BzKTtcclxuXHJcbiAgICAvLyBFQ1IgUmVwb3NpdG9yeSBmb3IgRGFzaGJvYXJkIGltYWdlcyAtIGltcG9ydCBleGlzdGluZyBpZiBpdCBleGlzdHNcclxuICAgIGNvbnN0IHJlcG9zaXRvcnkgPSBlY3IuUmVwb3NpdG9yeS5mcm9tUmVwb3NpdG9yeU5hbWUoXHJcbiAgICAgIHRoaXMsXHJcbiAgICAgICdEYXNoYm9hcmRSZXBvc2l0b3J5JyxcclxuICAgICAgJ3ppbGxvLWRhc2hib2FyZCdcclxuICAgICk7XHJcblxyXG4gICAgLy8gR2l0SHViIE9JREMgUHJvdmlkZXIgKGNoZWNrIGlmIGFscmVhZHkgZXhpc3RzKVxyXG4gICAgY29uc3QgZ2l0aHViUHJvdmlkZXIgPSBuZXcgaWFtLk9wZW5JZENvbm5lY3RQcm92aWRlcih0aGlzLCAnR2l0SHViT0lEQ1Byb3ZpZGVyJywge1xyXG4gICAgICB1cmw6ICdodHRwczovL3Rva2VuLmFjdGlvbnMuZ2l0aHVidXNlcmNvbnRlbnQuY29tJyxcclxuICAgICAgY2xpZW50SWRzOiBbJ3N0cy5hbWF6b25hd3MuY29tJ10sXHJcbiAgICAgIHRodW1icHJpbnRzOiBbJzY5MzhmZDRkOThiYWIwM2ZhYWRiOTdiMzQzOTY4MzFlMzc4MGFlYTEnLCAnMWM1OGEzYTg1MThlODc1OWJmMDc1Yjc2Yjc1MGQ0ZjJkZjI2NGZjZCddLFxyXG4gICAgfSk7XHJcblxyXG4gICAgLy8gSUFNIFJvbGUgZm9yIEdpdEh1YiBBY3Rpb25zXHJcbiAgICBjb25zdCBnaXRodWJBY3Rpb25zUm9sZSA9IG5ldyBpYW0uUm9sZSh0aGlzLCAnR2l0SHViQWN0aW9uc1JvbGUnLCB7XHJcbiAgICAgIHJvbGVOYW1lOiAnWmlsbG8tR2l0SHViQWN0aW9ucy1Sb2xlJyxcclxuICAgICAgYXNzdW1lZEJ5OiBuZXcgaWFtLkZlZGVyYXRlZFByaW5jaXBhbChcclxuICAgICAgICBnaXRodWJQcm92aWRlci5vcGVuSWRDb25uZWN0UHJvdmlkZXJBcm4sXHJcbiAgICAgICAge1xyXG4gICAgICAgICAgU3RyaW5nRXF1YWxzOiB7XHJcbiAgICAgICAgICAgICd0b2tlbi5hY3Rpb25zLmdpdGh1YnVzZXJjb250ZW50LmNvbTphdWQnOiAnc3RzLmFtYXpvbmF3cy5jb20nLFxyXG4gICAgICAgICAgfSxcclxuICAgICAgICAgIFN0cmluZ0xpa2U6IHtcclxuICAgICAgICAgICAgJ3Rva2VuLmFjdGlvbnMuZ2l0aHVidXNlcmNvbnRlbnQuY29tOnN1Yic6IGByZXBvOiR7cHJvcHMuZ2l0aHViT3duZXJ9LyR7cHJvcHMuZ2l0aHViUmVwb306KmAsXHJcbiAgICAgICAgICB9LFxyXG4gICAgICAgIH0sXHJcbiAgICAgICAgJ3N0czpBc3N1bWVSb2xlV2l0aFdlYklkZW50aXR5J1xyXG4gICAgICApLFxyXG4gICAgfSk7XHJcblxyXG4gICAgLy8gR3JhbnQgRUNSIHBlcm1pc3Npb25zIHRvIEdpdEh1YiBBY3Rpb25zIHJvbGVcclxuICAgIHJlcG9zaXRvcnkuZ3JhbnRQdWxsUHVzaChnaXRodWJBY3Rpb25zUm9sZSk7XHJcblxyXG4gICAgLy8gQWxzbyBuZWVkIEdldEF1dGhvcml6YXRpb25Ub2tlbiBmb3IgZG9ja2VyIGxvZ2luXHJcbiAgICBnaXRodWJBY3Rpb25zUm9sZS5hZGRUb1BvbGljeShuZXcgaWFtLlBvbGljeVN0YXRlbWVudCh7XHJcbiAgICAgIGVmZmVjdDogaWFtLkVmZmVjdC5BTExPVyxcclxuICAgICAgYWN0aW9uczogWydlY3I6R2V0QXV0aG9yaXphdGlvblRva2VuJ10sXHJcbiAgICAgIHJlc291cmNlczogWycqJ10sXHJcbiAgICB9KSk7XHJcblxyXG4gICAgLy8gQ3JlYXRlIFNlY3JldHMgTWFuYWdlciBzZWNyZXRzIChlbXB0eSAtIHRvIGJlIHBvcHVsYXRlZCBtYW51YWxseSlcclxuICAgIGNvbnN0IGRhc2hib2FyZFNlY3JldHMgPSBuZXcgc2VjcmV0c21hbmFnZXIuU2VjcmV0KHRoaXMsICdEYXNoYm9hcmRTZWNyZXRzJywge1xyXG4gICAgICBzZWNyZXROYW1lOiAnWmlsbG8vZGFzaGJvYXJkJyxcclxuICAgICAgZGVzY3JpcHRpb246ICdEYXNoYm9hcmQgYXBwbGljYXRpb24gc2VjcmV0cyAoU3VwYWJhc2UsIFN0cmlwZSwgZXRjLiknLFxyXG4gICAgICBnZW5lcmF0ZVNlY3JldFN0cmluZzoge1xyXG4gICAgICAgIHNlY3JldFN0cmluZ1RlbXBsYXRlOiBKU09OLnN0cmluZ2lmeSh7XHJcbiAgICAgICAgICAnU3VwYWJhc2VfX1VybCc6ICdSRVBMQUNFX01FJyxcclxuICAgICAgICAgICdTdXBhYmFzZV9fS2V5JzogJ1JFUExBQ0VfTUUnLFxyXG4gICAgICAgICAgJ1N0cmlwZV9fU2VjcmV0S2V5JzogJ1JFUExBQ0VfTUUnLFxyXG4gICAgICAgICAgJ1N0cmlwZV9fUHVibGlzaGFibGVLZXknOiAnUkVQTEFDRV9NRScsXHJcbiAgICAgICAgICAnR29vZ2xlX19NYXBzQXBpS2V5JzogJycsXHJcbiAgICAgICAgICAnV2FsbGV0X19BcHBsZV9fQ2VydGlmaWNhdGVQYXNzd29yZCc6ICdSRVBMQUNFX01FJyxcclxuICAgICAgICAgICdXYWxsZXRfX1dlYlNlcnZpY2VVcmwnOiAnaHR0cHM6Ly9kYXNoYm9hcmQuemlsbG8uYXBwJyxcclxuICAgICAgICB9KSxcclxuICAgICAgICBnZW5lcmF0ZVN0cmluZ0tleTogJ2R1bW15JywgLy8gUmVxdWlyZWQgYnV0IHdlJ2xsIGRlbGV0ZSBpdFxyXG4gICAgICB9LFxyXG4gICAgfSk7XHJcblxyXG4gICAgY29uc3QgYXBwbGVQMTJTZWNyZXQgPSBuZXcgc2VjcmV0c21hbmFnZXIuU2VjcmV0KHRoaXMsICdBcHBsZVAxMlNlY3JldCcsIHtcclxuICAgICAgc2VjcmV0TmFtZTogJ1ppbGxvL2NlcnRzL2FwcGxlLXAxMicsXHJcbiAgICAgIGRlc2NyaXB0aW9uOiAnQXBwbGUgV2FsbGV0IFAxMiBjZXJ0aWZpY2F0ZSAoYmFzZTY0IGVuY29kZWQpJyxcclxuICAgIH0pO1xyXG5cclxuICAgIGNvbnN0IGdvb2dsZUtleVNlY3JldCA9IG5ldyBzZWNyZXRzbWFuYWdlci5TZWNyZXQodGhpcywgJ0dvb2dsZUtleVNlY3JldCcsIHtcclxuICAgICAgc2VjcmV0TmFtZTogJ1ppbGxvL2NlcnRzL2dvb2dsZS1rZXknLFxyXG4gICAgICBkZXNjcmlwdGlvbjogJ0dvb2dsZSBXYWxsZXQgc2VydmljZSBhY2NvdW50IGtleSBKU09OJyxcclxuICAgIH0pO1xyXG5cclxuICAgIC8vIEFwcCBSdW5uZXIgSW5zdGFuY2UgUm9sZSAoZm9yIGFjY2Vzc2luZyBTZWNyZXRzIE1hbmFnZXIpXHJcbiAgICBjb25zdCBhcHBSdW5uZXJJbnN0YW5jZVJvbGUgPSBuZXcgaWFtLlJvbGUodGhpcywgJ0FwcFJ1bm5lckluc3RhbmNlUm9sZScsIHtcclxuICAgICAgcm9sZU5hbWU6ICdaaWxsby1BcHBSdW5uZXItSW5zdGFuY2UtUm9sZScsXHJcbiAgICAgIGFzc3VtZWRCeTogbmV3IGlhbS5TZXJ2aWNlUHJpbmNpcGFsKCd0YXNrcy5hcHBydW5uZXIuYW1hem9uYXdzLmNvbScpLFxyXG4gICAgfSk7XHJcblxyXG4gICAgLy8gR3JhbnQgc2VjcmV0cyBhY2Nlc3NcclxuICAgIGRhc2hib2FyZFNlY3JldHMuZ3JhbnRSZWFkKGFwcFJ1bm5lckluc3RhbmNlUm9sZSk7XHJcbiAgICBhcHBsZVAxMlNlY3JldC5ncmFudFJlYWQoYXBwUnVubmVySW5zdGFuY2VSb2xlKTtcclxuICAgIGdvb2dsZUtleVNlY3JldC5ncmFudFJlYWQoYXBwUnVubmVySW5zdGFuY2VSb2xlKTtcclxuXHJcbiAgICAvLyBBcHAgUnVubmVyIEVDUiBBY2Nlc3MgUm9sZVxyXG4gICAgY29uc3QgYXBwUnVubmVyQWNjZXNzUm9sZSA9IG5ldyBpYW0uUm9sZSh0aGlzLCAnQXBwUnVubmVyQWNjZXNzUm9sZScsIHtcclxuICAgICAgcm9sZU5hbWU6ICdaaWxsby1BcHBSdW5uZXItRUNSLUFjY2Vzcy1Sb2xlJyxcclxuICAgICAgYXNzdW1lZEJ5OiBuZXcgaWFtLlNlcnZpY2VQcmluY2lwYWwoJ2J1aWxkLmFwcHJ1bm5lci5hbWF6b25hd3MuY29tJyksXHJcbiAgICB9KTtcclxuICAgIHJlcG9zaXRvcnkuZ3JhbnRQdWxsKGFwcFJ1bm5lckFjY2Vzc1JvbGUpO1xyXG5cclxuICAgIC8vIE91dHB1dHNcclxuICAgIG5ldyBjZGsuQ2ZuT3V0cHV0KHRoaXMsICdFQ1JSZXBvc2l0b3J5VXJpJywge1xyXG4gICAgICB2YWx1ZTogcmVwb3NpdG9yeS5yZXBvc2l0b3J5VXJpLFxyXG4gICAgICBkZXNjcmlwdGlvbjogJ0VDUiBSZXBvc2l0b3J5IFVSSScsXHJcbiAgICAgIGV4cG9ydE5hbWU6ICdaaWxsby1FQ1ItVVJJJyxcclxuICAgIH0pO1xyXG5cclxuICAgIG5ldyBjZGsuQ2ZuT3V0cHV0KHRoaXMsICdHaXRIdWJBY3Rpb25zUm9sZUFybicsIHtcclxuICAgICAgdmFsdWU6IGdpdGh1YkFjdGlvbnNSb2xlLnJvbGVBcm4sXHJcbiAgICAgIGRlc2NyaXB0aW9uOiAnSUFNIFJvbGUgQVJOIGZvciBHaXRIdWIgQWN0aW9ucyAoYWRkIHRvIHJlcG8gc2VjcmV0cyBhcyBBV1NfUk9MRV9BUk4pJyxcclxuICAgICAgZXhwb3J0TmFtZTogJ1ppbGxvLUdpdEh1Yi1Sb2xlLUFSTicsXHJcbiAgICB9KTtcclxuXHJcbiAgICBuZXcgY2RrLkNmbk91dHB1dCh0aGlzLCAnU2VjcmV0c01hbmFnZXJBcm4nLCB7XHJcbiAgICAgIHZhbHVlOiBkYXNoYm9hcmRTZWNyZXRzLnNlY3JldEFybixcclxuICAgICAgZGVzY3JpcHRpb246ICdTZWNyZXRzIE1hbmFnZXIgQVJOIC0gcG9wdWxhdGUgd2l0aCBhY3R1YWwgdmFsdWVzJyxcclxuICAgIH0pO1xyXG5cclxuICAgIC8vIEFwcCBSdW5uZXIgU2VydmljZSAtIG9ubHkgZGVwbG95IGFmdGVyIGZpcnN0IGltYWdlIGlzIHB1c2hlZFxyXG4gICAgaWYgKHByb3BzLmRlcGxveUFwcFJ1bm5lcikge1xyXG4gICAgICBjb25zdCBhcHBSdW5uZXJTZXJ2aWNlID0gbmV3IGFwcHJ1bm5lci5DZm5TZXJ2aWNlKHRoaXMsICdEYXNoYm9hcmRTZXJ2aWNlJywge1xyXG4gICAgICAgIHNlcnZpY2VOYW1lOiAnWmlsbG8tZGFzaGJvYXJkJyxcclxuICAgICAgICBzb3VyY2VDb25maWd1cmF0aW9uOiB7XHJcbiAgICAgICAgICBhdXRoZW50aWNhdGlvbkNvbmZpZ3VyYXRpb246IHtcclxuICAgICAgICAgICAgYWNjZXNzUm9sZUFybjogYXBwUnVubmVyQWNjZXNzUm9sZS5yb2xlQXJuLFxyXG4gICAgICAgICAgfSxcclxuICAgICAgICAgIGF1dG9EZXBsb3ltZW50c0VuYWJsZWQ6IHRydWUsXHJcbiAgICAgICAgICBpbWFnZVJlcG9zaXRvcnk6IHtcclxuICAgICAgICAgICAgaW1hZ2VJZGVudGlmaWVyOiBgJHtyZXBvc2l0b3J5LnJlcG9zaXRvcnlVcml9OmxhdGVzdGAsXHJcbiAgICAgICAgICAgIGltYWdlUmVwb3NpdG9yeVR5cGU6ICdFQ1InLFxyXG4gICAgICAgICAgICBpbWFnZUNvbmZpZ3VyYXRpb246IHtcclxuICAgICAgICAgICAgICBwb3J0OiAnODA4MCcsXHJcbiAgICAgICAgICAgICAgcnVudGltZUVudmlyb25tZW50U2VjcmV0czogW1xyXG4gICAgICAgICAgICAgICAge1xyXG4gICAgICAgICAgICAgICAgICBuYW1lOiAnQVBQX1NFQ1JFVFNfQVJOJyxcclxuICAgICAgICAgICAgICAgICAgdmFsdWU6IGRhc2hib2FyZFNlY3JldHMuc2VjcmV0QXJuLFxyXG4gICAgICAgICAgICAgICAgfSxcclxuICAgICAgICAgICAgICAgIHtcclxuICAgICAgICAgICAgICAgICAgbmFtZTogJ0FQUExFX1AxMl9TRUNSRVRfQVJOJyxcclxuICAgICAgICAgICAgICAgICAgdmFsdWU6IGFwcGxlUDEyU2VjcmV0LnNlY3JldEFybixcclxuICAgICAgICAgICAgICAgIH0sXHJcbiAgICAgICAgICAgICAgICB7XHJcbiAgICAgICAgICAgICAgICAgIG5hbWU6ICdHT09HTEVfS0VZX1NFQ1JFVF9BUk4nLFxyXG4gICAgICAgICAgICAgICAgICB2YWx1ZTogZ29vZ2xlS2V5U2VjcmV0LnNlY3JldEFybixcclxuICAgICAgICAgICAgICAgIH0sXHJcbiAgICAgICAgICAgICAgXSxcclxuICAgICAgICAgICAgICBydW50aW1lRW52aXJvbm1lbnRWYXJpYWJsZXM6IFtcclxuICAgICAgICAgICAgICAgIHtcclxuICAgICAgICAgICAgICAgICAgbmFtZTogJ0FTUE5FVENPUkVfRU5WSVJPTk1FTlQnLFxyXG4gICAgICAgICAgICAgICAgICB2YWx1ZTogJ1Byb2R1Y3Rpb24nLFxyXG4gICAgICAgICAgICAgICAgfSxcclxuICAgICAgICAgICAgICAgIHtcclxuICAgICAgICAgICAgICAgICAgbmFtZTogJ0FXU19SRUdJT04nLFxyXG4gICAgICAgICAgICAgICAgICB2YWx1ZTogJ3VzLWVhc3QtMScsXHJcbiAgICAgICAgICAgICAgICB9LFxyXG4gICAgICAgICAgICAgIF0sXHJcbiAgICAgICAgICAgIH0sXHJcbiAgICAgICAgICB9LFxyXG4gICAgICAgIH0sXHJcbiAgICAgICAgaW5zdGFuY2VDb25maWd1cmF0aW9uOiB7XHJcbiAgICAgICAgICBjcHU6ICcxMDI0JywgLy8gMSB2Q1BVXHJcbiAgICAgICAgICBtZW1vcnk6ICcyMDQ4JywgLy8gMiBHQlxyXG4gICAgICAgICAgaW5zdGFuY2VSb2xlQXJuOiBhcHBSdW5uZXJJbnN0YW5jZVJvbGUucm9sZUFybixcclxuICAgICAgICB9LFxyXG4gICAgICAgIGhlYWx0aENoZWNrQ29uZmlndXJhdGlvbjoge1xyXG4gICAgICAgICAgcHJvdG9jb2w6ICdIVFRQJyxcclxuICAgICAgICAgIHBhdGg6ICcvYXBpL2hlYWx0aCcsXHJcbiAgICAgICAgICBpbnRlcnZhbDogMTAsXHJcbiAgICAgICAgICB0aW1lb3V0OiA1LFxyXG4gICAgICAgICAgaGVhbHRoeVRocmVzaG9sZDogMSxcclxuICAgICAgICAgIHVuaGVhbHRoeVRocmVzaG9sZDogNSxcclxuICAgICAgICB9LFxyXG4gICAgICB9KTtcclxuXHJcbiAgICAgIGFwcFJ1bm5lclNlcnZpY2Uubm9kZS5hZGREZXBlbmRlbmN5KGFwcFJ1bm5lckFjY2Vzc1JvbGUpO1xyXG5cclxuICAgICAgbmV3IGNkay5DZm5PdXRwdXQodGhpcywgJ0FwcFJ1bm5lclNlcnZpY2VVcmwnLCB7XHJcbiAgICAgICAgdmFsdWU6IGBodHRwczovLyR7YXBwUnVubmVyU2VydmljZS5hdHRyU2VydmljZVVybH1gLFxyXG4gICAgICAgIGRlc2NyaXB0aW9uOiAnQXBwIFJ1bm5lciBTZXJ2aWNlIFVSTCcsXHJcbiAgICAgIH0pO1xyXG5cclxuICAgICAgbmV3IGNkay5DZm5PdXRwdXQodGhpcywgJ05leHRTdGVwcycsIHtcclxuICAgICAgICB2YWx1ZTogYFxyXG4xLiBBZGQgY3VzdG9tIGRvbWFpbiBpbiBBcHAgUnVubmVyIGNvbnNvbGU6ICR7cHJvcHMuZG9tYWluTmFtZX1cclxuMi4gQWRkIFJvdXRlIDUzIENOQU1FIHJlY29yZCBmb3IgdGhlIGN1c3RvbSBkb21haW5cclxuYCxcclxuICAgICAgICBkZXNjcmlwdGlvbjogJ1NldHVwIGluc3RydWN0aW9ucycsXHJcbiAgICAgIH0pO1xyXG4gICAgfSBlbHNlIHtcclxuICAgICAgbmV3IGNkay5DZm5PdXRwdXQodGhpcywgJ05leHRTdGVwcycsIHtcclxuICAgICAgICB2YWx1ZTogYFxyXG5QaGFzZSAxIGNvbXBsZXRlISBOb3c6XHJcbjEuIEFkZCBHaXRIdWIgc2VjcmV0OiBBV1NfUk9MRV9BUk4gKHNlZSBHaXRIdWJBY3Rpb25zUm9sZUFybiBvdXRwdXQpXHJcbjIuIFBvcHVsYXRlIHNlY3JldHMgaW4gQVdTIFNlY3JldHMgTWFuYWdlcjogJHtkYXNoYm9hcmRTZWNyZXRzLnNlY3JldE5hbWV9XHJcbjMuIFB1c2ggZmlyc3QgRG9ja2VyIGltYWdlIHZpYSBHaXRIdWIgQWN0aW9ucyAoY3JlYXRlIHByb2QtcmVsZWFzZSBicmFuY2gpXHJcbjQuIFNldCBkZXBsb3lBcHBSdW5uZXI6IHRydWUgaW4gaW5mcmEvYmluL2luZnJhLnRzIGFuZCBydW4gY2RrIGRlcGxveSBhZ2FpblxyXG5gLFxyXG4gICAgICAgIGRlc2NyaXB0aW9uOiAnU2V0dXAgaW5zdHJ1Y3Rpb25zJyxcclxuICAgICAgfSk7XHJcbiAgICB9XHJcbiAgfVxyXG59XHJcbiJdfQ==