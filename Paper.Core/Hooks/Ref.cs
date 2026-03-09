using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Paper.Core.Hooks
{
    /// <summary>
    /// A mutable reference object — like React's <c>useRef</c>.
    /// </summary>
    public sealed class Ref<T>
    {
        public T Current { get; set; }
        public Ref(T initial) { Current = initial; }
    }
}