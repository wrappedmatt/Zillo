import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
interface DashboardStackProps extends cdk.StackProps {
    domainName: string;
    hostedZoneName: string;
    githubOwner: string;
    githubRepo: string;
}
export declare class DashboardStack extends cdk.Stack {
    constructor(scope: Construct, id: string, props: DashboardStackProps);
}
export {};
