using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicPerfTest
{
    internal class DynaObj<T> : DynamicObject
    {
        public override DynamicMetaObject GetMetaObject(System.Linq.Expressions.Expression parameter) =>
            new DynaMetaDynamic(parameter, this);

        public override bool TryGetMember(GetMemberBinder binder, out object result) =>
            this.GetMember(binder.Name, out result);

        public bool GetMember(string key, out object result)
        {
            result = "dummy";
            return true;
        }
    }
}
