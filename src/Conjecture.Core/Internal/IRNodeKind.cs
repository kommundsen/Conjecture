// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core.Internal;

internal enum IRNodeKind { Integer, Boolean, Bytes, Float64 = 3, Float32 = 4, StringLength = 5, StringChar = 6, CommandStart = 7 }