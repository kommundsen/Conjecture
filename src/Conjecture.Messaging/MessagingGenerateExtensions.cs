// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Messaging;

/// <summary>Extension methods on <see cref="Strategy"/> for messaging interaction generation.</summary>
public static class MessagingGenerateExtensions
{
    extension(Strategy)
    {
        /// <summary>Returns a <see cref="MessagingGenerateBuilder"/> for composing message interaction strategies.</summary>
        public static MessagingGenerateBuilder Messaging => new();
    }
}