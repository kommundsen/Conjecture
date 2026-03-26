using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Conjecture.Benchmarks.CoreDrawBenchmarks).Assembly).Run(args);
