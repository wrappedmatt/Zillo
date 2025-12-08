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
exports.ApiStack = void 0;
const cdk = __importStar(require("aws-cdk-lib"));
const ecr = __importStar(require("aws-cdk-lib/aws-ecr"));
const iam = __importStar(require("aws-cdk-lib/aws-iam"));
const apprunner = __importStar(require("aws-cdk-lib/aws-apprunner"));
const secretsmanager = __importStar(require("aws-cdk-lib/aws-secretsmanager"));
class ApiStack extends cdk.Stack {
    constructor(scope, id, props) {
        super(scope, id, props);
        // Create ECR Repository for API images
        const repository = new ecr.Repository(this, 'ApiRepository', {
            repositoryName: 'zillo-api',
            removalPolicy: cdk.RemovalPolicy.RETAIN,
            lifecycleRules: [
                {
                    maxImageCount: 10,
                    description: 'Keep only 10 images',
                },
            ],
        });
        // Import existing GitHub OIDC role (created by dashboard stack)
        const githubActionsRole = iam.Role.fromRoleName(this, 'GitHubActionsRole', 'Zillo-GitHubActions-Role');
        // Grant ECR permissions to GitHub Actions role
        repository.grantPullPush(githubActionsRole);
        // Import dashboard secrets (reuse same credentials)
        const dashboardSecrets = props.dashboardSecretsArn
            ? secretsmanager.Secret.fromSecretCompleteArn(this, 'DashboardSecrets', props.dashboardSecretsArn)
            : secretsmanager.Secret.fromSecretNameV2(this, 'DashboardSecretsName', 'Zillo/dashboard');
        // App Runner ECR Access Role
        const appRunnerAccessRole = new iam.Role(this, 'AppRunnerAccessRole', {
            roleName: 'Zillo-API-AppRunner-ECR-Access-Role',
            assumedBy: new iam.ServicePrincipal('build.apprunner.amazonaws.com'),
        });
        repository.grantPull(appRunnerAccessRole);
        // App Runner Instance Role (for accessing Secrets Manager)
        const appRunnerInstanceRole = new iam.Role(this, 'AppRunnerInstanceRole', {
            roleName: 'Zillo-API-AppRunner-Instance-Role',
            assumedBy: new iam.ServicePrincipal('tasks.apprunner.amazonaws.com'),
        });
        // Grant secrets access
        dashboardSecrets.grantRead(appRunnerInstanceRole);
        // Outputs
        new cdk.CfnOutput(this, 'ECRRepositoryUri', {
            value: repository.repositoryUri,
            description: 'ECR Repository URI for API',
            exportName: 'Zillo-API-ECR-URI',
        });
        // App Runner Service - only deploy after first image is pushed
        if (props.deployAppRunner) {
            const appRunnerService = new apprunner.CfnService(this, 'ApiService', {
                serviceName: 'Zillo-api',
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
                    cpu: '512', // 0.5 vCPU (API is lightweight)
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
        }
        else {
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
exports.ApiStack = ApiStack;
//# sourceMappingURL=data:application/json;base64,eyJ2ZXJzaW9uIjozLCJmaWxlIjoiYXBpLXN0YWNrLmpzIiwic291cmNlUm9vdCI6IiIsInNvdXJjZXMiOlsiYXBpLXN0YWNrLnRzIl0sIm5hbWVzIjpbXSwibWFwcGluZ3MiOiI7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7O0FBQUEsaURBQW1DO0FBQ25DLHlEQUEyQztBQUMzQyx5REFBMkM7QUFDM0MscUVBQXVEO0FBQ3ZELCtFQUFpRTtBQVdqRSxNQUFhLFFBQVMsU0FBUSxHQUFHLENBQUMsS0FBSztJQUNyQyxZQUFZLEtBQWdCLEVBQUUsRUFBVSxFQUFFLEtBQW9CO1FBQzVELEtBQUssQ0FBQyxLQUFLLEVBQUUsRUFBRSxFQUFFLEtBQUssQ0FBQyxDQUFDO1FBRXhCLHVDQUF1QztRQUN2QyxNQUFNLFVBQVUsR0FBRyxJQUFJLEdBQUcsQ0FBQyxVQUFVLENBQUMsSUFBSSxFQUFFLGVBQWUsRUFBRTtZQUMzRCxjQUFjLEVBQUUsV0FBVztZQUMzQixhQUFhLEVBQUUsR0FBRyxDQUFDLGFBQWEsQ0FBQyxNQUFNO1lBQ3ZDLGNBQWMsRUFBRTtnQkFDZDtvQkFDRSxhQUFhLEVBQUUsRUFBRTtvQkFDakIsV0FBVyxFQUFFLHFCQUFxQjtpQkFDbkM7YUFDRjtTQUNGLENBQUMsQ0FBQztRQUVILGdFQUFnRTtRQUNoRSxNQUFNLGlCQUFpQixHQUFHLEdBQUcsQ0FBQyxJQUFJLENBQUMsWUFBWSxDQUM3QyxJQUFJLEVBQ0osbUJBQW1CLEVBQ25CLDBCQUEwQixDQUMzQixDQUFDO1FBRUYsK0NBQStDO1FBQy9DLFVBQVUsQ0FBQyxhQUFhLENBQUMsaUJBQWlCLENBQUMsQ0FBQztRQUU1QyxvREFBb0Q7UUFDcEQsTUFBTSxnQkFBZ0IsR0FBRyxLQUFLLENBQUMsbUJBQW1CO1lBQ2hELENBQUMsQ0FBQyxjQUFjLENBQUMsTUFBTSxDQUFDLHFCQUFxQixDQUFDLElBQUksRUFBRSxrQkFBa0IsRUFBRSxLQUFLLENBQUMsbUJBQW1CLENBQUM7WUFDbEcsQ0FBQyxDQUFDLGNBQWMsQ0FBQyxNQUFNLENBQUMsZ0JBQWdC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QUFXLEVBQUU7Z0JBQ25DLEtBQUssRUFBRTs4Q0FDK0IsS0FBSyxDQUFDLFVBQVU7O0NBRTdEO2dCQUNPLFdBQVcsRUFBRSxvQkFBb0I7YUFDbEMsQ0FBQyxDQUFDO1FBQ0wsQ0FBQzthQUFNLENBQUM7WUFDTixJQUFJLEdBQUcsQ0FBQyxTQUFTLENBQUMsSUFBSSxFQUFFLFdBQVcsRUFBRTtnQkFDbkMsS0FBSyxFQUFFOzs7O0NBSWQ7Z0JBQ08sV0FBVyxFQUFFLG9CQUFvQjthQUNsQyxDQUFDLENBQUM7UUFDTCxDQUFDO0lBQ0gsQ0FBQztDQUNGO0FBL0hELDRCQStIQyIsInNvdXJjZXNDb250ZW50IjpbImltcG9ydCAqIGFzIGNkayBmcm9tICdhd3MtY2RrLWxpYic7XHJcbmltcG9ydCAqIGFzIGVjciBmcm9tICdhd3MtY2RrLWxpYi9hd3MtZWNyJztcclxuaW1wb3J0ICogYXMgaWFtIGZyb20gJ2F3cy1jZGstbGliL2F3cy1pYW0nO1xyXG5pbXBvcnQgKiBhcyBhcHBydW5uZXIgZnJvbSAnYXdzLWNkay1saWIvYXdzLWFwcHJ1bm5lcic7XHJcbmltcG9ydCAqIGFzIHNlY3JldHNtYW5hZ2VyIGZyb20gJ2F3cy1jZGstbGliL2F3cy1zZWNyZXRzbWFuYWdlcic7XHJcbmltcG9ydCB7IENvbnN0cnVjdCB9IGZyb20gJ2NvbnN0cnVjdHMnO1xyXG5cclxuaW50ZXJmYWNlIEFwaVN0YWNrUHJvcHMgZXh0ZW5kcyBjZGsuU3RhY2tQcm9wcyB7XHJcbiAgZG9tYWluTmFtZTogc3RyaW5nO1xyXG4gIC8qKiBTZXQgdG8gdHJ1ZSBhZnRlciBmaXJzdCBpbWFnZSBpcyBwdXNoZWQgdG8gRUNSICovXHJcbiAgZGVwbG95QXBwUnVubmVyPzogYm9vbGVhbjtcclxuICAvKiogUmVmZXJlbmNlIHRvIGRhc2hib2FyZCBzZWNyZXRzIChyZXVzZSBzYW1lIHNlY3JldHMpICovXHJcbiAgZGFzaGJvYXJkU2VjcmV0c0Fybj86IHN0cmluZztcclxufVxyXG5cclxuZXhwb3J0IGNsYXNzIEFwaVN0YWNrIGV4dGVuZHMgY2RrLlN0YWNrIHtcclxuICBjb25zdHJ1Y3RvcihzY29wZTogQ29uc3RydWN0LCBpZDogc3RyaW5nLCBwcm9wczogQXBpU3RhY2tQcm9wcykge1xyXG4gICAgc3VwZXIoc2NvcGUsIGlkLCBwcm9wcyk7XHJcblxyXG4gICAgLy8gQ3JlYXRlIEVDUiBSZXBvc2l0b3J5IGZvciBBUEkgaW1hZ2VzXHJcbiAgICBjb25zdCByZXBvc2l0b3J5ID0gbmV3IGVjci5SZXBvc2l0b3J5KHRoaXMsICdBcGlSZXBvc2l0b3J5Jywge1xyXG4gICAgICByZXBvc2l0b3J5TmFtZTogJ3ppbGxvLWFwaScsXHJcbiAgICAgIHJlbW92YWxQb2xpY3k6IGNkay5SZW1vdmFsUG9saWN5LlJFVEFJTixcclxuICAgICAgbGlmZWN5Y2xlUnVsZXM6IFtcclxuICAgICAgICB7XHJcbiAgICAgICAgICBtYXhJbWFnZUNvdW50OiAxMCxcclxuICAgICAgICAgIGRlc2NyaXB0aW9uOiAnS2VlcCBvbmx5IDEwIGltYWdlcycsXHJcbiAgICAgICAgfSxcclxuICAgICAgXSxcclxuICAgIH0pO1xyXG5cclxuICAgIC8vIEltcG9ydCBleGlzdGluZyBHaXRIdWIgT0lEQyByb2xlIChjcmVhdGVkIGJ5IGRhc2hib2FyZCBzdGFjaylcclxuICAgIGNvbnN0IGdpdGh1YkFjdGlvbnNSb2xlID0gaWFtLlJvbGUuZnJvbVJvbGVOYW1lKFxyXG4gICAgICB0aGlzLFxyXG4gICAgICAnR2l0SHViQWN0aW9uc1JvbGUnLFxyXG4gICAgICAnWmlsbG8tR2l0SHViQWN0aW9ucy1Sb2xlJ1xyXG4gICAgKTtcclxuXHJcbiAgICAvLyBHcmFudCBFQ1IgcGVybWlzc2lvbnMgdG8gR2l0SHViIEFjdGlvbnMgcm9sZVxyXG4gICAgcmVwb3NpdG9yeS5ncmFudFB1bGxQdXNoKGdpdGh1YkFjdGlvbnNSb2xlKTtcclxuXHJcbiAgICAvLyBJbXBvcnQgZGFzaGJvYXJkIHNlY3JldHMgKHJldXNlIHNhbWUgY3JlZGVudGlhbHMpXHJcbiAgICBjb25zdCBkYXNoYm9hcmRTZWNyZXRzID0gcHJvcHMuZGFzaGJvYXJkU2VjcmV0c0FyblxyXG4gICAgICA/IHNlY3JldHNtYW5hZ2VyLlNlY3JldC5mcm9tU2VjcmV0Q29tcGxldGVBcm4odGhpcywgJ0Rhc2hib2FyZFNlY3JldHMnLCBwcm9wcy5kYXNoYm9hcmRTZWNyZXRzQXJuKVxyXG4gICAgICA6IHNlY3JldHNtYW5hZ2VyLlNlY3JldC5mcm9tU2VjcmV0TmFtZVYyKHRoaXMsICdEYXNoYm9hcmRTZWNyZXRzTmFtZScsICdaaWxsby9kYXNoYm9hcmQnKTtcclxuXHJcbiAgICAvLyBBcHAgUnVubmVyIEVDUiBBY2Nlc3MgUm9sZVxyXG4gICAgY29uc3QgYXBwUnVubmVyQWNjZXNzUm9sZSA9IG5ldyBpYW0uUm9sZSh0aGlzLCAnQXBwUnVubmVyQWNjZXNzUm9sZScsIHtcclxuICAgICAgcm9sZU5hbWU6ICdaaWxsby1BUEktQXBwUnVubmVyLUVDUi1BY2Nlc3MtUm9sZScsXHJcbiAgICAgIGFzc3VtZWRCeTogbmV3IGlhbS5TZXJ2aWNlUHJpbmNpcGFsKCdidWlsZC5hcHBydW5uZXIuYW1hem9uYXdzLmNvbScpLFxyXG4gICAgfSk7XHJcbiAgICByZXBvc2l0b3J5LmdyYW50UHVsbChhcHBSdW5uZXJBY2Nlc3NSb2xlKTtcclxuXHJcbiAgICAvLyBBcHAgUnVubmVyIEluc3RhbmNlIFJvbGUgKGZvciBhY2Nlc3NpbmcgU2VjcmV0cyBNYW5hZ2VyKVxyXG4gICAgY29uc3QgYXBwUnVubmVySW5zdGFuY2VSb2xlID0gbmV3IGlhbS5Sb2xlKHRoaXMsICdBcHBSdW5uZXJJbnN0YW5jZVJvbGUnLCB7XHJcbiAgICAgIHJvbGVOYW1lOiAnWmlsbG8tQVBJLUFwcFJ1bm5lci1JbnN0YW5jZS1Sb2xlJyxcclxuICAgICAgYXNzdW1lZEJ5OiBuZXcgaWFtLlNlcnZpY2VQcmluY2lwYWwoJ3Rhc2tzLmFwcHJ1bm5lci5hbWF6b25hd3MuY29tJyksXHJcbiAgICB9KTtcclxuXHJcbiAgICAvLyBHcmFudCBzZWNyZXRzIGFjY2Vzc1xyXG4gICAgZGFzaGJvYXJkU2VjcmV0cy5ncmFudFJlYWQoYXBwUnVubmVySW5zdGFuY2VSb2xlKTtcclxuXHJcbiAgICAvLyBPdXRwdXRzXHJcbiAgICBuZXcgY2RrLkNmbk91dHB1dCh0aGlzLCAnRUNSUmVwb3NpdG9yeVVyaScsIHtcclxuICAgICAgdmFsdWU6IHJlcG9zaXRvcnkucmVwb3NpdG9yeVVyaSxcclxuICAgICAgZGVzY3JpcHRpb246ICdFQ1IgUmVwb3NpdG9yeSBVUkkgZm9yIEFQSScsXHJcbiAgICAgIGV4cG9ydE5hbWU6ICdaaWxsby1BUEktRUNSLVVSSScsXHJcbiAgICB9KTtcclxuXHJcbiAgICAvLyBBcHAgUnVubmVyIFNlcnZpY2UgLSBvbmx5IGRlcGxveSBhZnRlciBmaXJzdCBpbWFnZSBpcyBwdXNoZWRcclxuICAgIGlmIChwcm9wcy5kZXBsb3lBcHBSdW5uZXIpIHtcclxuICAgICAgY29uc3QgYXBwUnVubmVyU2VydmljZSA9IG5ldyBhcHBydW5uZXIuQ2ZuU2VydmljZSh0aGlzLCAnQXBpU2VydmljZScsIHtcclxuICAgICAgICBzZXJ2aWNlTmFtZTogJ1ppbGxvLWFwaScsXHJcbiAgICAgICAgc291cmNlQ29uZmlndXJhdGlvbjoge1xyXG4gICAgICAgICAgYXV0aGVudGljYXRpb25Db25maWd1cmF0aW9uOiB7XHJcbiAgICAgICAgICAgIGFjY2Vzc1JvbGVBcm46IGFwcFJ1bm5lckFjY2Vzc1JvbGUucm9sZUFybixcclxuICAgICAgICAgIH0sXHJcbiAgICAgICAgICBhdXRvRGVwbG95bWVudHNFbmFibGVkOiB0cnVlLFxyXG4gICAgICAgICAgaW1hZ2VSZXBvc2l0b3J5OiB7XHJcbiAgICAgICAgICAgIGltYWdlSWRlbnRpZmllcjogYCR7cmVwb3NpdG9yeS5yZXBvc2l0b3J5VXJpfTpsYXRlc3RgLFxyXG4gICAgICAgICAgICBpbWFnZVJlcG9zaXRvcnlUeXBlOiAnRUNSJyxcclxuICAgICAgICAgICAgaW1hZ2VDb25maWd1cmF0aW9uOiB7XHJcbiAgICAgICAgICAgICAgcG9ydDogJzgwODAnLFxyXG4gICAgICAgICAgICAgIHJ1bnRpbWVFbnZpcm9ubWVudFNlY3JldHM6IFtcclxuICAgICAgICAgICAgICAgIHtcclxuICAgICAgICAgICAgICAgICAgbmFtZTogJ0FQUF9TRUNSRVRTX0FSTicsXHJcbiAgICAgICAgICAgICAgICAgIHZhbHVlOiBkYXNoYm9hcmRTZWNyZXRzLnNlY3JldEFybixcclxuICAgICAgICAgICAgICAgIH0sXHJcbiAgICAgICAgICAgICAgXSxcclxuICAgICAgICAgICAgICBydW50aW1lRW52aXJvbm1lbnRWYXJpYWJsZXM6IFtcclxuICAgICAgICAgICAgICAgIHtcclxuICAgICAgICAgICAgICAgICAgbmFtZTogJ0FTUE5FVENPUkVfRU5WSVJPTk1FTlQnLFxyXG4gICAgICAgICAgICAgICAgICB2YWx1ZTogJ1Byb2R1Y3Rpb24nLFxyXG4gICAgICAgICAgICAgICAgfSxcclxuICAgICAgICAgICAgICAgIHtcclxuICAgICAgICAgICAgICAgICAgbmFtZTogJ0FXU19SRUdJT04nLFxyXG4gICAgICAgICAgICAgICAgICB2YWx1ZTogJ3VzLWVhc3QtMScsXHJcbiAgICAgICAgICAgICAgICB9LFxyXG4gICAgICAgICAgICAgIF0sXHJcbiAgICAgICAgICAgIH0sXHJcbiAgICAgICAgICB9LFxyXG4gICAgICAgIH0sXHJcbiAgICAgICAgaW5zdGFuY2VDb25maWd1cmF0aW9uOiB7XHJcbiAgICAgICAgICBjcHU6ICc1MTInLCAvLyAwLjUgdkNQVSAoQVBJIGlzIGxpZ2h0d2VpZ2h0KVxyXG4gICAgICAgICAgbWVtb3J5OiAnMTAyNCcsIC8vIDEgR0JcclxuICAgICAgICAgIGluc3RhbmNlUm9sZUFybjogYXBwUnVubmVySW5zdGFuY2VSb2xlLnJvbGVBcm4sXHJcbiAgICAgICAgfSxcclxuICAgICAgICBoZWFsdGhDaGVja0NvbmZpZ3VyYXRpb246IHtcclxuICAgICAgICAgIHByb3RvY29sOiAnSFRUUCcsXHJcbiAgICAgICAgICBwYXRoOiAnL2FwaS9oZWFsdGgnLFxyXG4gICAgICAgICAgaW50ZXJ2YWw6IDEwLFxyXG4gICAgICAgICAgdGltZW91dDogNSxcclxuICAgICAgICAgIGhlYWx0aHlUaHJlc2hvbGQ6IDEsXHJcbiAgICAgICAgICB1bmhlYWx0aHlUaHJlc2hvbGQ6IDUsXHJcbiAgICAgICAgfSxcclxuICAgICAgfSk7XHJcblxyXG4gICAgICBhcHBSdW5uZXJTZXJ2aWNlLm5vZGUuYWRkRGVwZW5kZW5jeShhcHBSdW5uZXJBY2Nlc3NSb2xlKTtcclxuXHJcbiAgICAgIG5ldyBjZGsuQ2ZuT3V0cHV0KHRoaXMsICdBcHBSdW5uZXJTZXJ2aWNlVXJsJywge1xyXG4gICAgICAgIHZhbHVlOiBgaHR0cHM6Ly8ke2FwcFJ1bm5lclNlcnZpY2UuYXR0clNlcnZpY2VVcmx9YCxcclxuICAgICAgICBkZXNjcmlwdGlvbjogJ0FwcCBSdW5uZXIgU2VydmljZSBVUkwnLFxyXG4gICAgICB9KTtcclxuXHJcbiAgICAgIG5ldyBjZGsuQ2ZuT3V0cHV0KHRoaXMsICdOZXh0U3RlcHMnLCB7XHJcbiAgICAgICAgdmFsdWU6IGBcclxuMS4gQWRkIGN1c3RvbSBkb21haW4gaW4gQXBwIFJ1bm5lciBjb25zb2xlOiAke3Byb3BzLmRvbWFpbk5hbWV9XHJcbjIuIEFkZCBSb3V0ZSA1MyBDTkFNRSByZWNvcmQgZm9yIHRoZSBjdXN0b20gZG9tYWluXHJcbmAsXHJcbiAgICAgICAgZGVzY3JpcHRpb246ICdTZXR1cCBpbnN0cnVjdGlvbnMnLFxyXG4gICAgICB9KTtcclxuICAgIH0gZWxzZSB7XHJcbiAgICAgIG5ldyBjZGsuQ2ZuT3V0cHV0KHRoaXMsICdOZXh0U3RlcHMnLCB7XHJcbiAgICAgICAgdmFsdWU6IGBcclxuUGhhc2UgMSBjb21wbGV0ZSEgTm93OlxyXG4xLiBQdXNoIGZpcnN0IERvY2tlciBpbWFnZSB2aWEgR2l0SHViIEFjdGlvbnNcclxuMi4gU2V0IGRlcGxveUFwcFJ1bm5lcjogdHJ1ZSBhbmQgcnVuIGNkayBkZXBsb3kgYWdhaW5cclxuYCxcclxuICAgICAgICBkZXNjcmlwdGlvbjogJ1NldHVwIGluc3RydWN0aW9ucycsXHJcbiAgICAgIH0pO1xyXG4gICAgfVxyXG4gIH1cclxufVxyXG4iXX0=