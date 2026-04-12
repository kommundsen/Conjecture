// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.IO;
using System.Threading.Tasks;

using Conjecture.Core;

using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Formatting;

namespace Conjecture.Interactive;

/// <summary>Registers Conjecture formatters with the .NET Interactive kernel.</summary>
public sealed class ConjectureKernelExtension : IKernelExtension
{
    /// <summary>Registers HTML formatters for Conjecture types and completes.</summary>
    public Task OnLoadAsync(Kernel kernel)
    {
        Formatter.Register(
            typeof(Strategy<>),
            static (object obj, TextWriter writer) => writer.Write(StrategyHtmlFormatter.Format(obj)),
            HtmlFormatter.MimeType);

        return Task.CompletedTask;
    }
}