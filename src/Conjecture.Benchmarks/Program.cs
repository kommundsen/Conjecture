using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Conjecture.Benchmarks.CoreGenerationBenchmarks).Assembly).Run(args);
