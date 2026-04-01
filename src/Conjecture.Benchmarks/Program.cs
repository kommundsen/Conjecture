// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Conjecture.Benchmarks.CoreGenerationBenchmarks).Assembly).Run(args);