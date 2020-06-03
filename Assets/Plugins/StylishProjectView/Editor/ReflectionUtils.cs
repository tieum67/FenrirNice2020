using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace StylishProjectView
{
    public static class ReflectionUtils
    {
        public static T MakeFunc<T>(this MethodInfo method) where T : class
        {
            return Delegate.CreateDelegate(typeof(T), method) as T;
        }

        public static T MakeStaticFunc<T>(this MethodInfo method) where T : class
        {
            return Delegate.CreateDelegate(typeof(T), null, method) as T;
        }

        public static T MakeFuncGenericThis<T>(this MethodInfo method) where T : class
        {
            var obj = Expression.Parameter(typeof(object), "obj");
            var item = Expression.Convert(obj, method.DeclaringType);
            var call = Expression.Call(item, method);
            var lambda = Expression.Lambda<T>(call, obj);
            return lambda.Compile();
        }

        public static T MakeFuncGenericThisInput1<T>(this MethodInfo method) where T : class
        {
            var obj = Expression.Parameter(typeof(object), "obj");
            var item = Expression.Convert(obj, method.DeclaringType);
            var input1 = Expression.Parameter(method.GetParameters()[0].ParameterType, "input1");
            var call = Expression.Call(item, method, input1);
            var lambda = Expression.Lambda<T>(call, obj, input1);
            return lambda.Compile();
        }

        public static T MakeFuncGenericThisInput3<T>(this MethodInfo method) where T : class
        {
            var obj = Expression.Parameter(typeof(object), "obj");
            var item = Expression.Convert(obj, method.DeclaringType);
            var input1 = Expression.Parameter(method.GetParameters()[0].ParameterType, "input1");
            var input2 = Expression.Parameter(method.GetParameters()[1].ParameterType, "input2");
            var input3 = Expression.Parameter(method.GetParameters()[2].ParameterType, "input3");
            var call = Expression.Call(item, method, input1, input2, input3);
            var lambda = Expression.Lambda<T>(call, obj, input1, input2, input3);
            return lambda.Compile();
        }

        public static T MakeStaticFuncGenericInput<T>(this MethodInfo method) where T : class
        {
            var obj = Expression.Parameter(typeof(object), "input");
            var item = Expression.Convert(obj, method.GetParameters()[0].ParameterType);
            var call = Expression.Call(method, item);
            var lambda = Expression.Lambda<T>(call, obj);
            return lambda.Compile();
        }

        public static T MakeFuncGenericInput<T>(this MethodInfo method) where T : class
        {
            var obj = Expression.Parameter(typeof(object), "obj");
            var item = Expression.Convert(obj, method.DeclaringType);
            var obj2 = Expression.Parameter(typeof(object), "input");
            var item2 = Expression.Convert(obj2, method.GetParameters()[0].ParameterType);
            var call = Expression.Call(item, method, item2);
            var lambda = Expression.Lambda<T>(call, obj, obj2);
            return lambda.Compile();
        }

        public static Func<object, object> MakeGetter(this FieldInfo field)
        {
            var name = field.ReflectedType.FullName + ".get_" + field.Name;
            var method = new DynamicMethod(name, typeof(object), new[] { typeof(object) }, field.Module, true);
            var il = method.GetILGenerator();
            if (field.IsStatic)
            {
                il.Emit(OpCodes.Ldsfld, field);
                il.Emit(field.FieldType.IsClass ? OpCodes.Castclass : OpCodes.Box, field.FieldType);
            }
            else
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, field.DeclaringType);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(field.FieldType.IsClass ? OpCodes.Castclass : OpCodes.Box, field.FieldType);
            }
            il.Emit(OpCodes.Ret);
            return (Func<object, object>)method.CreateDelegate(typeof(Func<object, object>));
        }

        public static Action<object, object> MakeSetter(this FieldInfo field)
        {
            var name = field.ReflectedType.FullName + ".set_" + field.Name;
            var method = new DynamicMethod(name, null, new[] { typeof(object), typeof(object) }, field.Module, true);
            var il = method.GetILGenerator();
            if (field.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(field.FieldType.IsClass ? OpCodes.Castclass : OpCodes.Unbox_Any, field.FieldType);
                il.Emit(OpCodes.Stsfld, field);
            }
            else
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, field.DeclaringType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(field.FieldType.IsClass ? OpCodes.Castclass : OpCodes.Unbox_Any, field.FieldType);
                il.Emit(OpCodes.Stfld, field);
            }
            il.Emit(OpCodes.Ret);
            return (Action<object, object>)method.CreateDelegate(typeof(Action<object, object>));
        }
    }
}