using Xunit;

// Several suites read/write the global static Balance config; run tests serially so
// transient balance mutations in BalanceTests can't race other suites' Balance reads.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
