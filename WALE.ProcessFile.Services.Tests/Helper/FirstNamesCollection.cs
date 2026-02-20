using Meziantou.Xunit;

namespace WALE.ProcessFile.Services.Tests.Helper;

[CollectionDefinition("First Names 1", DisableParallelization = false)]
[EnableParallelization] // This enables the parallel execution of classes in a collection
public class FirstNamesCollection1 : ICollectionFixture<BaseFixture>
{
}

[CollectionDefinition("First Names 2", DisableParallelization = false)]
[EnableParallelization] // This enables the parallel execution of classes in a collection
public class FirstNamesCollection2 : ICollectionFixture<SingletonFirstNamesFixture>
{
}

[CollectionDefinition("First Names 3", DisableParallelization = false)]
[EnableParallelization] // This enables the parallel execution of classes in a collection
public class FirstNamesCollection3 : ICollectionFixture<SingletonFirstNamesFixture>
{
}

[CollectionDefinition("First Names 4", DisableParallelization = false)]
[EnableParallelization] // This enables the parallel execution of classes in a collection
public class FirstNamesCollection4 : ICollectionFixture<SingletonFirstNamesFixture>
{
}

[CollectionDefinition("First Names 5", DisableParallelization = false)]
public class FirstNamesCollection5 : ICollectionFixture<SingletonFirstNamesFixture>
{
}