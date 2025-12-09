#!/usr/bin/env node
import 'source-map-support/register';
import * as cdk from 'aws-cdk-lib';
import { DashboardStack } from '../lib/dashboard-stack';
import { PublicSiteStack } from '../lib/public-site-stack';
import { ApiStack } from '../lib/api-stack';
import { ApiEcsStack } from '../lib/api-ecs-stack';
import { RewardsStack } from '../lib/rewards-stack';

const app = new cdk.App();

const dashboardStack = new DashboardStack(app, 'ZilloDashboardStack', {
  env: {
    account: process.env.CDK_DEFAULT_ACCOUNT,
    region: 'us-east-1',
  },
  domainName: 'dashboard.zillo.app',
  hostedZoneName: 'zillo.app',
  githubOwner: 'wrappedmatt',
  githubRepo: 'Zillo',
  // Set to true after first Docker image is pushed to ECR
  deployAppRunner: true,
});

// API service - Phase 1: ECR repository (App Runner blocked by account limit)
new ApiStack(app, 'ZilloApiStack', {
  env: {
    account: process.env.CDK_DEFAULT_ACCOUNT,
    region: 'us-east-1',
  },
  domainName: 'api.zillo.app',
  deployAppRunner: false, // Using ECS instead
});

// API service - ECS Fargate (low latency for terminal API)
new ApiEcsStack(app, 'ZilloApiEcsStack', {
  env: {
    account: process.env.CDK_DEFAULT_ACCOUNT,
    region: 'us-east-1',
  },
  domainName: 'api.zillo.app',
  hostedZoneName: 'zillo.app',
});

// Public marketing site (S3 + CloudFront)
new PublicSiteStack(app, 'ZilloPublicSiteStack', {
  env: {
    account: process.env.CDK_DEFAULT_ACCOUNT,
    region: 'us-east-1',
  },
  domainName: 'zillohq.com',
  hostedZoneName: 'zillohq.com',
  githubActionsRoleArn: `arn:aws:iam::${process.env.CDK_DEFAULT_ACCOUNT}:role/Zillo-GitHubActions-Role`,
});

// Rewards service (App Runner)
new RewardsStack(app, 'ZilloRewardsStack', {
  env: {
    account: process.env.CDK_DEFAULT_ACCOUNT,
    region: 'us-east-1',
  },
  domainName: 'rewards.zillo.app',
  deployAppRunner: true,
});
