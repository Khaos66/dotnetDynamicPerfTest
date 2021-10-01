using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DynamicPerfTest
{
    internal class DynaMetaDynamic : DynamicMetaObject
    {
        private static readonly Expression[] NoArgs = Array.Empty<Expression>();
        private delegate DynamicMetaObject Fallback(DynamicMetaObject errorSuggestion);

        internal DynaMetaDynamic(Expression expression, DynamicObject value)
            : base(expression, BindingRestrictions.Empty, value)
        {
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
            if (IsOverridden("TryGetMember"))
            {
                return CallMethodWithResult("TryGetMember", binder, NoArgs, (e) =>
                    // this is where the debugger pauses, when it hangs
                    binder.FallbackGetMember(this, e));
            }

            return base.BindGetMember(binder);
        }

        /// <summary>
        /// Checks if the derived type has overridden the specified method.  If there is no
        /// implementation for the method provided then Dynamic falls back to the base class
        /// behavior which lets the call site determine how the binder is performed.
        /// </summary>
        private bool IsOverridden(string method)
        {
            var methods = Value.GetType().GetMember(method, MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance);

            foreach (MethodInfo mi in methods)
            {
                if (mi.DeclaringType != typeof(DynamicObject) && mi.GetBaseDefinition().DeclaringType == typeof(DynamicObject))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Helper method for generating a MetaObject which calls a
        /// specific method on Dynamic that returns a result
        /// </summary>
        private DynamicMetaObject CallMethodWithResult(string methodName, DynamicMetaObjectBinder binder, Expression[] args, Fallback fallback)
        {
            return CallMethodWithResult(methodName, binder, args, fallback, null);
        }

        /// <summary>
        /// Helper method for generating a MetaObject which calls a
        /// specific method on Dynamic that returns a result
        /// </summary>
        private DynamicMetaObject CallMethodWithResult(string methodName, DynamicMetaObjectBinder binder, Expression[] args, Fallback fallback, Fallback fallbackInvoke)
        {
            //
            // First, call fallback to do default binding
            // This produces either an error or a call to a .NET member
            //
            DynamicMetaObject fallbackResult = fallback(null);

            var callDynamic = BuildCallMethodWithResult(methodName, binder, args, fallbackResult, fallbackInvoke);

            //
            // Now, call fallback again using our new MO as the error
            // When we do this, one of two things can happen:
            //   1. Binding will succeed, and it will ignore our call to
            //      the dynamic method, OR
            //   2. Binding will fail, and it will use the MO we created
            //      above.
            //
            return fallback(callDynamic);
        }

        /// <summary>
        /// Helper method for generating a MetaObject which calls a
        /// specific method on DynamicObject that returns a result.
        ///
        /// args is either an array of arguments to be passed
        /// to the method as an object[] or NoArgs to signify that
        /// the target method takes no parameters.
        /// </summary>
        private DynamicMetaObject BuildCallMethodWithResult(string methodName, DynamicMetaObjectBinder binder, Expression[] args, DynamicMetaObject fallbackResult, Fallback fallbackInvoke)
        {
            if (!IsOverridden(methodName))
            {
                return fallbackResult;
            }

            //
            // Build a new expression like:
            // {
            //   object result;
            //   TryGetMember(payload, out result) ? fallbackInvoke(result) : fallbackResult
            // }
            //
            var result = Expression.Parameter(typeof(object), null);
            ParameterExpression callArgs = methodName != "TryBinaryOperation" ? Expression.Parameter(typeof(object[]), null) : Expression.Parameter(typeof(object), null);
            var callArgsValue = GetConvertedArgs(args);

            var resultMO = new DynamicMetaObject(result, BindingRestrictions.Empty);

            // Need to add a conversion if calling TryConvert
            if (binder.ReturnType != typeof(object))
            {
                // Debug.Assert(binder is ConvertBinder && fallbackInvoke == null);

                var convert = Expression.Convert(resultMO.Expression, binder.ReturnType);
                // will always be a cast or unbox
                Debug.Assert(convert.Method == null);

                // Prepare a good exception message in case the convert will fail
                string convertFailed = "Konversion fehlgeschlagen";

                var checkedConvert = Expression.Condition(
                    Expression.TypeIs(resultMO.Expression, binder.ReturnType),
                    convert,
                    Expression.Throw(
                        Expression.New(typeof(InvalidCastException).GetConstructor(new Type[] { typeof(string) }),
                            Expression.Call(
                                typeof(string).GetMethod("Format", new Type[] { typeof(string), typeof(object) }),
                                Expression.Constant(convertFailed),
                                Expression.Condition(
                                    Expression.Equal(resultMO.Expression, Expression.Constant(null)),
                                    Expression.Constant("null"),
                                    Expression.Call(
                                        resultMO.Expression,
                                        typeof(object).GetMethod("GetType")
                                    ),
                                    typeof(object)
                                )
                            )
                        ),
                        binder.ReturnType
                    ),
                    binder.ReturnType
                );

                resultMO = new DynamicMetaObject(checkedConvert, resultMO.Restrictions);
            }

            if (fallbackInvoke != null)
            {
                resultMO = fallbackInvoke(resultMO);
            }

            return new DynamicMetaObject(
                Expression.Block(
                    new[] { result, callArgs },
                    methodName != "TryBinaryOperation"
                        ? Expression.Assign(callArgs, Expression.NewArrayInit(typeof(object), callArgsValue))
                        : Expression.Assign(callArgs, callArgsValue[0]),
                    Expression.Condition(
                        Expression.Call(
                            GetLimitedSelf(),
                            typeof(DynamicObject).GetMethod(methodName),
                            BuildCallArgs(
                                binder,
                                args,
                                callArgs,
                                result
                            )
                        ),
                        Expression.Block(
                            methodName != "TryBinaryOperation" ? ReferenceArgAssign(callArgs, args) : Expression.Empty(),
                            resultMO.Expression
                        ),
                        fallbackResult.Expression,
                        binder.ReturnType
                    )
                ),
                GetRestrictions().Merge(resultMO.Restrictions).Merge(fallbackResult.Restrictions)
            );
        }

        /// <summary>
        /// Returns a Restrictions object which includes our current restrictions merged
        /// with a restriction limiting our type
        /// </summary>
        private BindingRestrictions GetRestrictions()
        {
            Debug.Assert(Restrictions == BindingRestrictions.Empty, "We don't merge, restrictions are always empty");

            return BindingRestrictions.Empty;
        }

        /// <summary>
        /// Helper method for generating expressions that assign byRef call
        /// parameters back to their original variables
        /// </summary>
        private static Expression ReferenceArgAssign(Expression callArgs, Expression[] args)
        {
            ReadOnlyCollectionBuilder<Expression>? block = null;

            for (var i = 0; i < args.Length; i++)
            {
                if (!((ParameterExpression)args[i]).IsByRef) continue;
                block ??= new ReadOnlyCollectionBuilder<Expression>();

                block.Add(
                    Expression.Assign(
                        args[i],
                        Expression.Convert(
                            Expression.ArrayIndex(
                                callArgs,
                                Expression.Constant(i)
                            ),
                            args[i].Type
                        )
                    )
                );
            }

            return block != null
                ? Expression.Block(block)
                : Expression.Empty();
        }

        /// <summary>
        /// Helper method for generating arguments for calling methods
        /// on DynamicObject.  parameters is either a list of ParameterExpressions
        /// to be passed to the method as an object[], or NoArgs to signify that
        /// the target method takes no object[] parameter.
        /// </summary>
        private static Expression[] BuildCallArgs(DynamicMetaObjectBinder binder, Expression[] parameters, Expression arg0, Expression arg1)
        {
            if (!object.ReferenceEquals(parameters, NoArgs))
                return arg1 != null ? new[] { Constant(binder), arg0, arg1 } : new[] { Constant(binder), arg0 };
            return arg1 != null ? new[] { Constant(binder), arg1 } : new Expression[] { Constant(binder) };
        }

        private static ConstantExpression Constant(DynamicMetaObjectBinder binder)
        {
            Type t = binder.GetType();
            while (!t.IsVisible)
            {
                t = t.BaseType;
            }
            return Expression.Constant(binder, t);
        }

        /// <summary>
        /// Returns our Expression converted to DynamicObject
        /// </summary>
        private Expression GetLimitedSelf()
        {
            // Convert to DynamicObject rather than LimitType, because
            // the limit type might be non-public.
            return typeof(DynamicObject).IsAssignableFrom(Expression.Type)
                ? Expression
                : Expression.Convert(Expression, typeof(DynamicObject));
        }

        private static Expression[] GetConvertedArgs(params Expression[] args)
        {
            ReadOnlyCollectionBuilder<Expression> paramArgs = new(args.Length);

            foreach (var t in args)
            {
                paramArgs.Add(Expression.Convert(t, typeof(object)));
            }

            return paramArgs.ToArray();
        }


    }
}
