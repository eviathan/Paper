using Xunit;

namespace Paper.Core.Tests;

/// <summary>
/// Runs all tests sequentially within this assembly to avoid races on
/// <see cref="Paper.Core.Hooks.RenderScheduler.OnRenderRequested"/>, which is a
/// shared static callback set by each <see cref="Paper.Core.Reconciler.Reconciler"/>
/// instance on construction.
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public sealed class SequentialCollection { }
