import * as cdk from 'aws-cdk-lib';
import * as s3 from 'aws-cdk-lib/aws-s3';
import * as cloudfront from 'aws-cdk-lib/aws-cloudfront';
import * as origins from 'aws-cdk-lib/aws-cloudfront-origins';
import * as iam from 'aws-cdk-lib/aws-iam';
import * as acm from 'aws-cdk-lib/aws-certificatemanager';
import * as route53 from 'aws-cdk-lib/aws-route53';
import * as route53Targets from 'aws-cdk-lib/aws-route53-targets';
import { Construct } from 'constructs';

interface PublicSiteStackProps extends cdk.StackProps {
  domainName: string;
  hostedZoneName: string;
  githubActionsRoleArn: string;
}

export class PublicSiteStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props: PublicSiteStackProps) {
    super(scope, id, props);

    // S3 bucket for static site hosting
    const siteBucket = new s3.Bucket(this, 'SiteBucket', {
      bucketName: `zillo-public-site-${this.account}`,
      blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
      removalPolicy: cdk.RemovalPolicy.RETAIN,
      encryption: s3.BucketEncryption.S3_MANAGED,
    });

    // Look up hosted zone
    const hostedZone = route53.HostedZone.fromLookup(this, 'HostedZone', {
      domainName: props.hostedZoneName,
    });

    // SSL Certificate (must be in us-east-1 for CloudFront)
    const certificate = new acm.Certificate(this, 'SiteCertificate', {
      domainName: props.domainName,
      validation: acm.CertificateValidation.fromDns(hostedZone),
    });

    // CloudFront Origin Access Identity
    const originAccessIdentity = new cloudfront.OriginAccessIdentity(this, 'OAI', {
      comment: 'Zillo Public Site OAI',
    });

    // Grant read access to CloudFront
    siteBucket.grantRead(originAccessIdentity);

    // CloudFront distribution
    const distribution = new cloudfront.Distribution(this, 'SiteDistribution', {
      defaultBehavior: {
        origin: new origins.S3Origin(siteBucket, {
          originAccessIdentity,
        }),
        viewerProtocolPolicy: cloudfront.ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
        cachePolicy: cloudfront.CachePolicy.CACHING_OPTIMIZED,
      },
      domainNames: [props.domainName],
      certificate,
      defaultRootObject: 'index.html',
      errorResponses: [
        {
          httpStatus: 404,
          responseHttpStatus: 200,
          responsePagePath: '/index.html',
          ttl: cdk.Duration.minutes(5),
        },
        {
          httpStatus: 403,
          responseHttpStatus: 200,
          responsePagePath: '/index.html',
          ttl: cdk.Duration.minutes(5),
        },
      ],
    });

    // Route 53 alias record
    new route53.ARecord(this, 'SiteAliasRecord', {
      zone: hostedZone,
      recordName: props.domainName,
      target: route53.RecordTarget.fromAlias(
        new route53Targets.CloudFrontTarget(distribution)
      ),
    });

    // Import GitHub Actions role and grant S3 permissions
    const githubActionsRole = iam.Role.fromRoleArn(
      this,
      'GitHubActionsRole',
      props.githubActionsRoleArn
    );

    siteBucket.grantReadWrite(githubActionsRole);

    // Grant CloudFront invalidation permissions
    githubActionsRole.addToPrincipalPolicy(new iam.PolicyStatement({
      effect: iam.Effect.ALLOW,
      actions: ['cloudfront:CreateInvalidation'],
      resources: [`arn:aws:cloudfront::${this.account}:distribution/${distribution.distributionId}`],
    }));

    // Outputs
    new cdk.CfnOutput(this, 'SiteBucketName', {
      value: siteBucket.bucketName,
      description: 'S3 bucket name for static site',
      exportName: 'Zillo-Public-Site-Bucket',
    });

    new cdk.CfnOutput(this, 'DistributionId', {
      value: distribution.distributionId,
      description: 'CloudFront distribution ID',
      exportName: 'Zillo-Public-Site-Distribution-Id',
    });

    new cdk.CfnOutput(this, 'SiteUrl', {
      value: `https://${props.domainName}`,
      description: 'Public site URL',
    });
  }
}
