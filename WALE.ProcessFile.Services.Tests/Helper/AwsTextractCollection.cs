namespace WALE.ProcessFile.Services.Tests.Helper;

[CollectionDefinition("AWS Textract 1", DisableParallelization = false)]
public class AwsTextractCollection1 : ICollectionFixture<SingletonAwsTextractFixture>
{
}

[CollectionDefinition("AWS Textract 2", DisableParallelization = false)]
public class AwsTextractCollection2 : ICollectionFixture<SingletonAwsTextractFixture>
{
}