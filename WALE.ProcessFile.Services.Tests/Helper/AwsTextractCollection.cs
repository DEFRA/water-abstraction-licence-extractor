namespace WALE.ProcessFile.Services.Tests.Helper;

[CollectionDefinition("AWS Textract 1")]
public class AwsTextractCollection1 : ICollectionFixture<SingletonAwsTextractFixture>
{
}

[CollectionDefinition("AWS Textract 2")]
public class AwsTextractCollection2 : ICollectionFixture<SingletonAwsTextractFixture>
{
}