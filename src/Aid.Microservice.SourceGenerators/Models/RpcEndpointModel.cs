using System;
using System.Collections.Generic;
using System.Linq;

namespace Aid.Microservice.SourceGenerators.Models;

public enum MethodReturnTypeKind
{
    Void,
    TaskVoid,
    ValueTaskVoid,
    TaskOfT,
    ValueTypeOfT,
    SyncOfT
}

public sealed class ParameterModel(
    string name,
    string typeFullName,
    bool isCancellationToken,
    bool hasDefaultValue,
    string? defaultValueLiteral,
    bool isNullable,
    bool isComplexType) : IEquatable<ParameterModel>
{
    public string Name { get; } = name;
    public string TypeFullName { get; } = typeFullName;
    public bool IsCancellationToken { get; } = isCancellationToken;
    public bool HasDefaultValue { get; } = hasDefaultValue;
    public string? DefaultValueLiteral { get; } = defaultValueLiteral;
    public bool IsNullable { get; } = isNullable;
    public bool IsComplexType { get; } = isComplexType;

    public bool Equals(ParameterModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name &&
               TypeFullName == other.TypeFullName &&
               IsCancellationToken == other.IsCancellationToken &&
               HasDefaultValue == other.HasDefaultValue &&
               DefaultValueLiteral == other.DefaultValueLiteral &&
               IsNullable == other.IsNullable &&
               IsComplexType == other.IsComplexType;
    }

    public override bool Equals(object? obj) => Equals(obj as ParameterModel);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Name.GetHashCode();
            hashCode = (hashCode * 397) ^ TypeFullName.GetHashCode();
            hashCode = (hashCode * 397) ^ IsCancellationToken.GetHashCode();
            hashCode = (hashCode * 397) ^ HasDefaultValue.GetHashCode();
            hashCode = (hashCode * 397) ^ (DefaultValueLiteral?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ IsNullable.GetHashCode();
            hashCode = (hashCode * 397) ^ IsComplexType.GetHashCode();
            return hashCode;
        }
    }
}

public sealed class MethodModel(
    string methodName,
    string rpcAlias,
    string? customSerializerTypeFullName,
    MethodReturnTypeKind returnTypeKind,
    string returnTypeFullName,
    IReadOnlyList<ParameterModel> parameters) : IEquatable<MethodModel>
{
    public string MethodName { get; } = methodName;
    public string RpcAlias { get; } = rpcAlias;
    public string? CustomSerializerTypeFullName { get; } = customSerializerTypeFullName;
    public MethodReturnTypeKind ReturnTypeKind { get; } = returnTypeKind;
    public string ReturnTypeFullName { get; } = returnTypeFullName;
    public IReadOnlyList<ParameterModel> Parameters { get; } = parameters;

    public bool Equals(MethodModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (MethodName != other.MethodName ||
            RpcAlias != other.RpcAlias ||
            CustomSerializerTypeFullName != other.CustomSerializerTypeFullName ||
            ReturnTypeKind != other.ReturnTypeKind ||
            ReturnTypeFullName != other.ReturnTypeFullName ||
            Parameters.Count != other.Parameters.Count)
        {
            return false;
        }

        for (int i = 0; i < Parameters.Count; i++)
        {
            if (!Parameters[i].Equals(other.Parameters[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as MethodModel);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = MethodName.GetHashCode();
            hashCode = (hashCode * 397) ^ RpcAlias.GetHashCode();
            hashCode = (hashCode * 397) ^ (CustomSerializerTypeFullName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (int)ReturnTypeKind;
            hashCode = (hashCode * 397) ^ ReturnTypeFullName.GetHashCode();
            hashCode = (hashCode * 397) ^ Parameters.Count;
            return hashCode;
        }
    }
}

public sealed class ServiceModel(
    string classFullName,
    string serviceName,
    string? customSerializerTypeFullName,
    string? explicitExchangeName,
    IReadOnlyList<string> explicitExchanges,
    IReadOnlyList<MethodModel> methods,
    bool isQueryHandler,
    string? queryName) : IEquatable<ServiceModel>
{
    public string ClassFullName { get; } = classFullName;
    public string ServiceName { get; } = serviceName;
    public string? CustomSerializerTypeFullName { get; } = customSerializerTypeFullName;
    public string? ExplicitExchangeName { get; } = explicitExchangeName;
    public IReadOnlyList<string> ExplicitExchanges { get; } = explicitExchanges;
    public IReadOnlyList<MethodModel> Methods { get; } = methods;
    public bool IsQueryHandler { get; } = isQueryHandler;
    public string? QueryName { get; } = queryName;

    public bool Equals(ServiceModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (ClassFullName != other.ClassFullName ||
            ServiceName != other.ServiceName ||
            CustomSerializerTypeFullName != other.CustomSerializerTypeFullName ||
            ExplicitExchangeName != other.ExplicitExchangeName ||
            IsQueryHandler != other.IsQueryHandler ||
            QueryName != other.QueryName ||
            ExplicitExchanges.Count != other.ExplicitExchanges.Count ||
            Methods.Count != other.Methods.Count)
        {
            return false;
        }

        if (ExplicitExchanges.Where((t, i) => t != other.ExplicitExchanges[i]).Any())
        {
            return false;
        }

        return !Methods.Where((t, i) => !t.Equals(other.Methods[i])).Any();
    }

    public override bool Equals(object? obj) => Equals(obj as ServiceModel);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = ClassFullName.GetHashCode();
            hashCode = (hashCode * 397) ^ ServiceName.GetHashCode();
            hashCode = (hashCode * 397) ^ (CustomSerializerTypeFullName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (ExplicitExchangeName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ IsQueryHandler.GetHashCode();
            hashCode = (hashCode * 397) ^ (QueryName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ Methods.Count;
            return hashCode;
        }
    }
}
