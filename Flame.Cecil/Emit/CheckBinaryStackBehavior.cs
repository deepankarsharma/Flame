﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flame.Cecil.Emit
{
    public class CheckBinaryStackBehavior : IStackBehavior
    {
        public void Apply(TypeStack Stack)
        {
            Stack.Pop();
            Stack.Pop();
            Stack.Push(PrimitiveTypes.Boolean);
        }
    }
}
