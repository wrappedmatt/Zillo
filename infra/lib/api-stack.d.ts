import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
interface ApiStackProps extends cdk.StackProps {
    domainName: string;
    /** Set to true after first image is pushed to ECR */
    deployAppRunner?: boolean;
    /** Reference to dashboard secrets (reuse same secrets) */
    dashboardSecretsArn?: string;
}
export declare class ApiStack extends cdk.Stack {
    constructor(scope: Construct, id: string, props: ApiStackProps);
}
export {};
