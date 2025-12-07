#!/usr/bin/env node
import 'source-map-support/register';
import * as cdk from 'aws-cdk-lib';
import { DashboardStack } from '../lib/dashboard-stack';

const app = new cdk.App();

new DashboardStack(app, 'ZilloDashboardStack', {
  env: {
    account: process.env.CDK_DEFAULT_ACCOUNT,
    region: 'us-east-1',
  },
  domainName: 'dashboard.zillo.app',
  hostedZoneName: 'zillo.app',
  githubOwner: 'wrappedmatt',
  githubRepo: 'Zillo',
});
