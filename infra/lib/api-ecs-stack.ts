import * as cdk from 'aws-cdk-lib';
import * as ec2 from 'aws-cdk-lib/aws-ec2';
import * as ecs from 'aws-cdk-lib/aws-ecs';
import * as ecr from 'aws-cdk-lib/aws-ecr';
import * as iam from 'aws-cdk-lib/aws-iam';
import * as elbv2 from 'aws-cdk-lib/aws-elasticloadbalancingv2';
import * as secretsmanager from 'aws-cdk-lib/aws-secretsmanager';
import * as logs from 'aws-cdk-lib/aws-logs';
import * as acm from 'aws-cdk-lib/aws-certificatemanager';
import * as route53 from 'aws-cdk-lib/aws-route53';
import * as route53Targets from 'aws-cdk-lib/aws-route53-targets';
import { Construct } from 'constructs';

interface ApiEcsStackProps extends cdk.StackProps {
  domainName: string;
  hostedZoneName?: string;
}

export class ApiEcsStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props: ApiEcsStackProps) {
    super(scope, id, props);

    // Import existing ECR repository (created by ApiStack Phase 1)
    const repository = ecr.Repository.fromRepositoryName(
      this,
      'ApiRepository',
      'zillo-api'
    );

    // Import existing secrets
    const secrets = secretsmanager.Secret.fromSecretNameV2(
      this,
      'DashboardSecrets',
      'Zillo/dashboard'
    );

    // Create VPC with public subnets only (cheaper, simpler)
    const vpc = new ec2.Vpc(this, 'ApiVpc', {
      vpcName: 'Zillo-API-VPC',
      maxAzs: 2,
      natGateways: 0, // No NAT to save costs
      subnetConfiguration: [
        {
          name: 'Public',
          subnetType: ec2.SubnetType.PUBLIC,
          cidrMask: 24,
        },
      ],
    });

    // Create ECS Cluster
    const cluster = new ecs.Cluster(this, 'ApiCluster', {
      clusterName: 'Zillo-API-Cluster',
      vpc,
      containerInsights: false, // Disable to save costs
    });

    // Task execution role (for pulling images and logging)
    const executionRole = new iam.Role(this, 'TaskExecutionRole', {
      roleName: 'Zillo-API-ECS-Execution-Role',
      assumedBy: new iam.ServicePrincipal('ecs-tasks.amazonaws.com'),
      managedPolicies: [
        iam.ManagedPolicy.fromAwsManagedPolicyName(
          'service-role/AmazonECSTaskExecutionRolePolicy'
        ),
      ],
    });

    // Grant secrets access to execution role
    secrets.grantRead(executionRole);

    // Task role (for application to access AWS services)
    const taskRole = new iam.Role(this, 'TaskRole', {
      roleName: 'Zillo-API-ECS-Task-Role',
      assumedBy: new iam.ServicePrincipal('ecs-tasks.amazonaws.com'),
    });

    // Grant secrets access to task role
    secrets.grantRead(taskRole);

    // CloudWatch Log Group
    const logGroup = new logs.LogGroup(this, 'ApiLogGroup', {
      logGroupName: '/ecs/zillo-api',
      retention: logs.RetentionDays.ONE_WEEK,
      removalPolicy: cdk.RemovalPolicy.DESTROY,
    });

    // Task Definition
    const taskDefinition = new ecs.FargateTaskDefinition(this, 'ApiTaskDef', {
      family: 'zillo-api',
      cpu: 256, // 0.25 vCPU
      memoryLimitMiB: 512, // 0.5 GB
      executionRole,
      taskRole,
    });

    // Container
    const container = taskDefinition.addContainer('api', {
      containerName: 'zillo-api',
      image: ecs.ContainerImage.fromEcrRepository(repository, 'latest'),
      logging: ecs.LogDrivers.awsLogs({
        streamPrefix: 'api',
        logGroup,
      }),
      environment: {
        ASPNETCORE_ENVIRONMENT: 'Production',
        ASPNETCORE_URLS: 'http://+:8080',
        AWS_REGION: 'us-east-1',
      },
      secrets: {
        APP_SECRETS_ARN: ecs.Secret.fromSecretsManager(secrets),
      },
      portMappings: [
        {
          containerPort: 8080,
          protocol: ecs.Protocol.TCP,
        },
      ],
      // Note: Container health check removed - using ALB health check instead
      // .NET containers don't have curl installed by default
    });

    // Security Group for ALB
    const albSecurityGroup = new ec2.SecurityGroup(this, 'AlbSecurityGroup', {
      vpc,
      securityGroupName: 'Zillo-API-ALB-SG',
      description: 'Security group for API ALB',
      allowAllOutbound: true,
    });
    albSecurityGroup.addIngressRule(
      ec2.Peer.anyIpv4(),
      ec2.Port.tcp(80),
      'Allow HTTP'
    );
    albSecurityGroup.addIngressRule(
      ec2.Peer.anyIpv4(),
      ec2.Port.tcp(443),
      'Allow HTTPS'
    );

    // Security Group for ECS Tasks
    const ecsSecurityGroup = new ec2.SecurityGroup(this, 'EcsSecurityGroup', {
      vpc,
      securityGroupName: 'Zillo-API-ECS-SG',
      description: 'Security group for API ECS tasks',
      allowAllOutbound: true,
    });
    ecsSecurityGroup.addIngressRule(
      albSecurityGroup,
      ec2.Port.tcp(8080),
      'Allow from ALB'
    );

    // Application Load Balancer
    const alb = new elbv2.ApplicationLoadBalancer(this, 'ApiAlb', {
      loadBalancerName: 'Zillo-API-ALB',
      vpc,
      internetFacing: true,
      securityGroup: albSecurityGroup,
    });

    // Target Group
    const targetGroup = new elbv2.ApplicationTargetGroup(this, 'ApiTargetGroup', {
      targetGroupName: 'Zillo-API-TG',
      vpc,
      port: 8080,
      protocol: elbv2.ApplicationProtocol.HTTP,
      targetType: elbv2.TargetType.IP,
      healthCheck: {
        path: '/api/health',
        interval: cdk.Duration.seconds(30),
        timeout: cdk.Duration.seconds(5),
        healthyThresholdCount: 2,
        unhealthyThresholdCount: 3,
      },
    });

    // Look up hosted zone for DNS
    const hostedZoneName = props.hostedZoneName || 'zillo.app';
    const hostedZone = route53.HostedZone.fromLookup(this, 'HostedZone', {
      domainName: hostedZoneName,
    });

    // Create ACM Certificate with DNS validation
    const certificate = new acm.Certificate(this, 'ApiCertificate', {
      domainName: props.domainName,
      validation: acm.CertificateValidation.fromDns(hostedZone),
    });

    // HTTPS Listener
    const httpsListener = alb.addListener('HttpsListener', {
      port: 443,
      certificates: [certificate],
      defaultAction: elbv2.ListenerAction.forward([targetGroup]),
    });

    // HTTP Listener - redirect to HTTPS
    const httpListener = alb.addListener('HttpListener', {
      port: 80,
      defaultAction: elbv2.ListenerAction.redirect({
        protocol: 'HTTPS',
        port: '443',
        permanent: true,
      }),
    });

    // Route 53 DNS record
    new route53.ARecord(this, 'ApiDnsRecord', {
      zone: hostedZone,
      recordName: props.domainName.split('.')[0], // 'api' from 'api.zillo.app'
      target: route53.RecordTarget.fromAlias(
        new route53Targets.LoadBalancerTarget(alb)
      ),
    });

    // ECS Service
    const service = new ecs.FargateService(this, 'ApiService', {
      serviceName: 'Zillo-API-Service',
      cluster,
      taskDefinition,
      desiredCount: 1,
      assignPublicIp: true, // Required for public subnets without NAT
      securityGroups: [ecsSecurityGroup],
      vpcSubnets: {
        subnetType: ec2.SubnetType.PUBLIC,
      },
      circuitBreaker: {
        rollback: true,
      },
    });

    // Attach service to target group
    service.attachToApplicationTargetGroup(targetGroup);

    // Auto-scaling (optional, for cost efficiency)
    const scaling = service.autoScaleTaskCount({
      minCapacity: 1,
      maxCapacity: 3,
    });

    scaling.scaleOnCpuUtilization('CpuScaling', {
      targetUtilizationPercent: 70,
      scaleInCooldown: cdk.Duration.seconds(60),
      scaleOutCooldown: cdk.Duration.seconds(60),
    });

    // Outputs
    new cdk.CfnOutput(this, 'AlbDnsName', {
      value: alb.loadBalancerDnsName,
      description: 'ALB DNS Name',
    });

    new cdk.CfnOutput(this, 'AlbArn', {
      value: alb.loadBalancerArn,
      description: 'ALB ARN (for adding HTTPS listener)',
    });

    new cdk.CfnOutput(this, 'ApiUrl', {
      value: `https://${props.domainName}`,
      description: 'API URL',
    });
  }
}
