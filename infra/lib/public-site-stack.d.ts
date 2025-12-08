import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
interface PublicSiteStackProps extends cdk.StackProps {
    domainName: string;
    hostedZoneName: string;
    githubActionsRoleArn: string;
}
export declare class PublicSiteStack extends cdk.Stack {
    constructor(scope: Construct, id: string, props: PublicSiteStackProps);
}
export {};
