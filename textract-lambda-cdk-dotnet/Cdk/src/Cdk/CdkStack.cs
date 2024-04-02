using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Constructs;

namespace Cdk
{
    public class CdkStack : Stack
    {
        readonly string lambdaFunctionName = "textractLambdaFunction";
        internal CdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            //Create S3 bucket
            var bucket = new Bucket(this, "textract-bucket", new BucketProps
            {
                BucketName = $"{this.Account}-textract-bucket"
            });

            //Lambda Function IAM role
            var lambdaIAMRole = new Role(this, "TextractLambdaRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                Description = "IAM Role for the lambda function",
                ManagedPolicies = new[]
                {
                        ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
                        ManagedPolicy.FromAwsManagedPolicyName("AmazonTextractFullAccess")
                }
            });

            //Grant S3 read & write access
            bucket.GrantReadWrite(lambdaIAMRole);

            // CloudWatch Log Group
            _ = new LogGroup(this, "CloudWatchLogs", new LogGroupProps
            {
                LogGroupName = $"/aws/lambda/{lambdaFunctionName}",
                RemovalPolicy = RemovalPolicy.DESTROY,
                Retention = RetentionDays.ONE_DAY
            });

            // Lambda Function Build Commands
            var buildOption = new BundlingOptions()
            {
                Image = Runtime.DOTNET_8.BundlingImage,
                User = "root",
                OutputType = BundlingOutput.ARCHIVED,
                Command = new string[]{
               "/bin/sh",
                "-c",
                " dotnet tool install -g Amazon.Lambda.Tools"+
                " && dotnet build"+
                " && dotnet lambda package --output-package /asset-output/function.zip"
                }
            };

            //Lambda function
            _ = new Function(this, "LambdaFunction", new FunctionProps
            {
                FunctionName = lambdaFunctionName,
                MemorySize = 512,
                Timeout = Duration.Seconds(30),
                Runtime = Runtime.DOTNET_8,
                Handler = "TextractLambda::TextractLambda.Function::FunctionHandler",
                Role = lambdaIAMRole,
                Code = Code.FromAsset("../TextractLambda/", new Amazon.CDK.AWS.S3.Assets.AssetOptions
                {
                    Bundling = buildOption
                }),
                Events = new[] {new S3EventSource(bucket, new S3EventSourceProps
                {
                    Events = new[] { EventType.OBJECT_CREATED },
                    Filters = new[] { new NotificationKeyFilter { Prefix = "input/" } }
                })}
            });
        }
    }
}