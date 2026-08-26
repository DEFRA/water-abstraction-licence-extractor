using Meziantou.Xunit;

namespace WALE.ProcessFile.Services.Tests.Helper;

[CollectionDefinition("PdfPigNoOcrPdfTests2", DisableParallelization = false)]
[EnableParallelization] // This enables the parallel execution of classes in a collection
public class FirstNamesCollection1B : ICollectionFixture<FirstNamesFixture>
{
}

[CollectionDefinition("First Names 2", DisableParallelization = false)]
[EnableParallelization] // This enables the parallel execution of classes in a collection
public class FirstNamesCollection2 : ICollectionFixture<FirstNamesFixture>
{
}

[CollectionDefinition("First Names 3", DisableParallelization = false)]
[EnableParallelization] // This enables the parallel execution of classes in a collection
public class FirstNamesCollection3 : ICollectionFixture<FirstNamesFixture>
{
}

[CollectionDefinition("First Names 4", DisableParallelization = false)]
[EnableParallelization] // This enables the parallel execution of classes in a collection
public class FirstNamesCollection4 : ICollectionFixture<FirstNamesFixture>
{
}

[CollectionDefinition("First Names 5", DisableParallelization = false)]
public class FirstNamesCollection5 : ICollectionFixture<FirstNamesFixture>
{
}