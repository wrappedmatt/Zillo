#!/usr/bin/env node
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
require("source-map-support/register");
const cdk = __importStar(require("aws-cdk-lib"));
const dashboard_stack_1 = require("../lib/dashboard-stack");
const public_site_stack_1 = require("../lib/public-site-stack");
const api_stack_1 = require("../lib/api-stack");
const app = new cdk.App();
const dashboardStack = new dashboard_stack_1.DashboardStack(app, 'ZilloDashboardStack', {
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
// API service (App Runner)
new api_stack_1.ApiStack(app, 'ZilloApiStack', {
    env: {
        account: process.env.CDK_DEFAULT_ACCOUNT,
        region: 'us-east-1',
    },
    domainName: 'api.zillo.app',
    // Set to true after first Docker image is pushed to ECR
    deployAppRunner: false,
});
// Public marketing site (S3 + CloudFront)
new public_site_stack_1.PublicSiteStack(app, 'ZilloPublicSiteStack', {
    env: {
        account: process.env.CDK_DEFAULT_ACCOUNT,
        region: 'us-east-1',
    },
    domainName: 'zillohq.com',
    hostedZoneName: 'zillohq.com',
    githubActionsRoleArn: `arn:aws:iam::${process.env.CDK_DEFAULT_ACCOUNT}:role/Zillo-GitHubActions-Role`,
});
//# sourceMappingURL=data:application/json;base64,eyJ2ZXJzaW9uIjozLCJmaWxlIjoiaW5mcmEuanMiLCJzb3VyY2VSb290IjoiIiwic291cmNlcyI6WyJpbmZyYS50cyJdLCJuYW1lcyI6W10sIm1hcHBpbmdzIjoiOzs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7OztBQUNBLHVDQUFxQztBQUNyQyxpREFBbUM7QUFDbkMsNERBQXdEO0FBQ3hELGdFQUEyRDtBQUMzRCxnREFBNEM7QUFFNUMsTUFBTSxHQUFHLEdBQUcsSUFBSSxHQUFHLENBQUMsR0FBRyxFQUFFLENBQUM7QUFFMUIsTUFBTSxjQUFjLEdBQUcsSUFBSSxnQ0FBYyxDQUFDLEdBQUcsRUFBRSxxQkFBcUIsRUFBRTtJQUNwRSxHQUFHLEVBQUU7UUFDSCxPQUFPLEVBQUUsT0FBTyxDQUFDLEdBQUcsQ0FBQyxtQkFBbUI7UUFDeEMsTUFBTSxFQUFFLFdBQVc7S0FDcEI7SUFDRCxVQUFVLEVBQUUscUJBQXFCO0lBQ2pDLGNBQWMsRUFBRSxXQUFXO0lBQzNCLFdBQVcsRUFBRSxhQUFhO0lBQzFCLFVBQVUsRUFBRSxPQUFPO0lBQ25CLHdEQUF3RDtJQUN4RCxlQUFlLEVBQUUsSUFBSTtDQUN0QixDQUFDLENBQUM7QUFFSCwyQkFBMkI7QUFDM0IsSUFBSSxvQkFBUSxDQUFDLEdBQUcsRUFBRSxlQUFlLEVBQUU7SUFDakMsR0FBRyxFQUFFO1FBQ0gsT0FBTyxFQUFFLE9BQU8sQ0FBQyxHQUFHLENBQUMsbUJBQW1CO1FBQ3hDLE1BQU0sRUFBRSxXQUFXO0tBQ3BCO0lBQ0QsVUFBVSxFQUFFLGVBQWU7SUFDM0Isd0RBQXdEO0lBQ3hELGVBQWUsRUFBRSxLQUFLO0NBQ3ZCLENBQUMsQ0FBQztBQUVILDBDQUEwQztBQUMxQyxJQUFJLG1DQUFlLENBQUMsR0FBRyxFQUFFLHNCQUFzQixFQUFFO0lBQy9DLEdBQUcsRUFBRTtRQUNILE9BQU8sRUFBRSxPQUFPLENBQUMsR0FBRyxDQUFDLG1CQUFtQjtRQUN4QyxNQUFNLEVBQUUsV0FBVztLQUNwQjtJQUNELFVBQVUsRUFBRSxhQUFhO0lBQ3pCLGNBQWMsRUFBRSxhQUFhO0lBQzdCLG9CQUFvQixFQUFFLGdCQUFnQixPQUFPLENBQUMsR0FBRyxDQUFDLG1CQUFtQixnQ0FBZ0M7Q0FDdEcsQ0FBQyxDQUFDIiwic291cmNlc0NvbnRlbnQiOlsiIyEvdXNyL2Jpbi9lbnYgbm9kZVxyXG5pbXBvcnQgJ3NvdXJjZS1tYXAtc3VwcG9ydC9yZWdpc3Rlcic7XHJcbmltcG9ydCAqIGFzIGNkayBmcm9tICdhd3MtY2RrLWxpYic7XHJcbmltcG9ydCB7IERhc2hib2FyZFN0YWNrIH0gZnJvbSAnLi4vbGliL2Rhc2hib2FyZC1zdGFjayc7XHJcbmltcG9ydCB7IFB1YmxpY1NpdGVTdGFjayB9IGZyb20gJy4uL2xpYi9wdWJsaWMtc2l0ZS1zdGFjayc7XHJcbmltcG9ydCB7IEFwaVN0YWNrIH0gZnJvbSAnLi4vbGliL2FwaS1zdGFjayc7XHJcblxyXG5jb25zdCBhcHAgPSBuZXcgY2RrLkFwcCgpO1xyXG5cclxuY29uc3QgZGFzaGJvYXJkU3RhY2sgPSBuZXcgRGFzaGJvYXJkU3RhY2soYXBwLCAnWmlsbG9EYXNoYm9hcmRTdGFjaycsIHtcclxuICBlbnY6IHtcclxuICAgIGFjY291bnQ6IHByb2Nlc3MuZW52LkNES19ERUZBVUxUX0FDQ09VTlQsXHJcbiAgICByZWdpb246ICd1cy1lYXN0LTEnLFxyXG4gIH0sXHJcbiAgZG9tYWluTmFtZTogJ2Rhc2hib2FyZC56aWxsby5hcHAnLFxyXG4gIGhvc3RlZFpvbmVOYW1lOiAnemlsbG8uYXBwJyxcclxuICBnaXRodWJPd25lcjogJ3dyYXBwZWRtYXR0JyxcclxuICBnaXRodWJSZXBvOiAnWmlsbG8nLFxyXG4gIC8vIFNldCB0byB0cnVlIGFmdGVyIGZpcnN0IERvY2tlciBpbWFnZSBpcyBwdXNoZWQgdG8gRUNSXHJcbiAgZGVwbG95QXBwUnVubmVyOiB0cnVlLFxyXG59KTtcclxuXHJcbi8vIEFQSSBzZXJ2aWNlIChBcHAgUnVubmVyKVxyXG5uZXcgQXBpU3RhY2soYXBwLCAnWmlsbG9BcGlTdGFjaycsIHtcclxuICBlbnY6IHtcclxuICAgIGFjY291bnQ6IHByb2Nlc3MuZW52LkNES19ERUZBVUxUX0FDQ09VTlQsXHJcbiAgICByZWdpb246ICd1cy1lYXN0LTEnLFxyXG4gIH0sXHJcbiAgZG9tYWluTmFtZTogJ2FwaS56aWxsby5hcHAnLFxyXG4gIC8vIFNldCB0byB0cnVlIGFmdGVyIGZpcnN0IERvY2tlciBpbWFnZSBpcyBwdXNoZWQgdG8gRUNSXHJcbiAgZGVwbG95QXBwUnVubmVyOiBmYWxzZSxcclxufSk7XHJcblxyXG4vLyBQdWJsaWMgbWFya2V0aW5nIHNpdGUgKFMzICsgQ2xvdWRGcm9udClcclxubmV3IFB1YmxpY1NpdGVTdGFjayhhcHAsICdaaWxsb1B1YmxpY1NpdGVTdGFjaycsIHtcclxuICBlbnY6IHtcclxuICAgIGFjY291bnQ6IHByb2Nlc3MuZW52LkNES19ERUZBVUxUX0FDQ09VTlQsXHJcbiAgICByZWdpb246ICd1cy1lYXN0LTEnLFxyXG4gIH0sXHJcbiAgZG9tYWluTmFtZTogJ3ppbGxvaHEuY29tJyxcclxuICBob3N0ZWRab25lTmFtZTogJ3ppbGxvaHEuY29tJyxcclxuICBnaXRodWJBY3Rpb25zUm9sZUFybjogYGFybjphd3M6aWFtOjoke3Byb2Nlc3MuZW52LkNES19ERUZBVUxUX0FDQ09VTlR9OnJvbGUvWmlsbG8tR2l0SHViQWN0aW9ucy1Sb2xlYCxcclxufSk7XHJcbiJdfQ==