using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;

namespace Finder.MsBuild.Task;

public partial class IdentifyCauseOfAssemblyReference
{
    private void LogMessage(string message) => Log.LogMessage(_messageImportance, message);
    private void LogError(string message) => Log.LogError(message);
    private void LogWarning(string message) => Log.LogWarning(message);

    private void LogVerbose(string message)
    {
        if (!Verbose) return;
        Log.LogMessage(_messageImportance, message);
    }

    private void ProcessType(TypeDef type, AssemblyRefMatch[] targetRefs, List<ITaskItem> foundCauses,
        string sourcePath)
    {
        var matchIndex = -1;
        
        LogVerbose($"Processing type: {type.FullName}");
        
        // Check type custom attributes for a matching reference.
        var typeName = type.FullName;
        
        if (type.HasCustomAttributes)
        {
            LogVerbose($"  Checking {type.CustomAttributes.Count} custom attributes on type {typeName}");
            CheckCustomAttributes(type.CustomAttributes, targetRefs, foundCauses, typeName, "Type", sourcePath);
        }

        // Process the field types.
        if (type.HasFields)
        {
            LogVerbose($"  Examining {type.Fields.Count} fields in {typeName}");
            foreach (var field in type.Fields)
            {
                if (!IsReferenceMatch(field.FieldType, targetRefs, out matchIndex)) continue;
                var matchName = targetRefs[matchIndex].ToString();
                var fieldName = field.Name;
                var fieldTypeName = field.FieldType.FullName;
                var message =
                    $"Field '{typeName}.{fieldName}' uses type '{fieldTypeName}' referencing {matchName}.";
                
                LogVerbose(message);

                AddReferenceTaskItem(
                    foundCauses,
                    sourcePath,
                    matchName,
                    typeName,
                    memberName: fieldName,
                    referenceType: "Field",
                    referencedType: fieldTypeName,
                    message: message);

                // Check field custom attributes
                if (field.HasCustomAttributes)
                {
                    LogVerbose($"    Checking {field.CustomAttributes.Count} custom attributes on field {fieldName}");
                    CheckCustomAttributes(field.CustomAttributes, targetRefs, foundCauses, $"{typeName}.{fieldName}", "Field",
                        sourcePath);
                }
            }
        }

        // Process the property types.
        if (type.HasProperties)
        {
            LogVerbose($"  Examining {type.Properties.Count} properties in {typeName}");
            foreach (var property in type.Properties)
            {
                // there is no property.PropertyType;
                var propertyType = property.GetMethod?.ReturnType;
                if (propertyType == null || !IsReferenceMatch(propertyType, targetRefs, out matchIndex)) continue;
                var matchName = targetRefs[matchIndex].ToString();
                var propertyName = property.Name;
                var propertyTypeName = propertyType.FullName;
                var message =
                    $"Property '{typeName}.{propertyName}' uses type '{propertyTypeName}' referencing {matchName}.";
                LogVerbose(message);

                AddReferenceTaskItem(
                    foundCauses,
                    sourcePath,
                    matchName,
                    typeName,
                    memberName: propertyName,
                    referenceType: "Property",
                    referencedType: propertyTypeName,
                    message: message);

                // Check property custom attributes
                if (property.HasCustomAttributes)
                {
                    LogVerbose($"    Checking {property.CustomAttributes.Count} custom attributes on property {propertyName}");
                    CheckCustomAttributes(property.CustomAttributes, targetRefs, foundCauses, $"{typeName}.{propertyName}",
                        "Property", sourcePath);
                }
            }
        }

        // Process the event types.
        if (type.HasEvents)
        {
            LogVerbose($"  Examining {type.Events.Count} events in {typeName}");
            foreach (var @event in type.Events)
            {
                if (!IsReferenceMatch(@event.EventType, targetRefs, out matchIndex)) continue;
                var matchName = targetRefs[matchIndex].ToString();
                var eventName = @event.Name;
                var eventTypeName = @event.EventType.FullName;
                var message =
                    $"Event '{typeName}.{eventName}' uses type '{eventTypeName}' referencing {matchName}.";
                LogVerbose(message);

                AddReferenceTaskItem(
                    foundCauses,
                    sourcePath,
                    matchName,
                    typeName,
                    memberName: eventName,
                    referenceType: "Event",
                    referencedType: eventTypeName,
                    message: message);

                // Check event custom attributes
                if (@event.HasCustomAttributes)
                {
                    LogVerbose($"    Checking {@event.CustomAttributes.Count} custom attributes on event {eventName}");
                    CheckCustomAttributes(@event.CustomAttributes, targetRefs, foundCauses, $"{typeName}.{eventName}", "Event",
                        sourcePath);
                }
            }
        }

        // Process each method.
        if (type.HasMethods)
        {
            LogVerbose($"  Examining {type.Methods.Count} methods in {typeName}");
            foreach (var method in type.Methods)
            {
                // Check method custom attributes.
                var methodName = method.Name;
                
                if (method.HasCustomAttributes)
                {
                    LogVerbose($"    Checking {method.CustomAttributes.Count} custom attributes on method {methodName}");
                    CheckCustomAttributes(method.CustomAttributes, targetRefs, foundCauses, $"{typeName}.{methodName}",
                        "Method", sourcePath);
                }

                // Check method parameters.
                if (method.Parameters is {Count: > 0})
                {
                    LogVerbose($"    Examining {method.Parameters.Count} parameters in method {methodName}");
                    for (var i = 0; i < method.Parameters.Count; i++)
                    {
                        var parameter = method.Parameters[i];
                        // Parameter.Name might be null, so use parameter.Type.
                        if (parameter.Type == null || !IsReferenceMatch(parameter.Type, targetRefs, out matchIndex)) continue;
                        var matchName = targetRefs[matchIndex].ToString();
                        var parameterName = parameter.Name ?? $"#{i}";
                        var parameterTypeName = parameter.Type.FullName;
                        var message =
                            $"Method '{typeName}.{methodName}' has parameter '{parameterName}' of type '{parameterTypeName}' referencing {matchName}.";
                        LogVerbose(message);

                        AddReferenceTaskItem(
                            foundCauses,
                            sourcePath,
                            matchName,
                            typeName,
                            memberName: methodName,
                            referenceType: "Parameter",
                            referencedType: parameterTypeName,
                            parameterName: parameterName,
                            parameterIndex: i.ToString(),
                            message: message);

                        if (parameter.ParamDef?.HasCustomAttributes != true) continue;

                        // Check parameter custom attributes
                        LogVerbose($"      Checking {parameter.ParamDef.CustomAttributes.Count} custom attributes on parameter {parameterName}");
                        CheckCustomAttributes(parameter.ParamDef.CustomAttributes, targetRefs, foundCauses,
                            $"{typeName}.{methodName}({parameterName})", "Parameter", sourcePath);
                    }
                }

                // Check method return type.
                if (method.MethodSig?.RetType is not null)
                {
                    LogVerbose($"    Checking return type of method {methodName}");
                    if (IsReferenceMatch(method.MethodSig.RetType, targetRefs, out matchIndex))
                    {
                        var matchName = targetRefs[matchIndex].ToString();
                        var returnTypeName = method.MethodSig.RetType.FullName;
                        var message =
                            $"Method '{typeName}.{methodName}' return type '{returnTypeName}' references {matchName}.";
                        LogVerbose(message);

                        AddReferenceTaskItem(
                            foundCauses,
                            sourcePath,
                            matchName,
                            typeName,
                            memberName: methodName,
                            referenceType: "ReturnType",
                            referencedType: returnTypeName,
                            message: message);
                    }
                }

                // Check instructions in the method body.
                if (!method.HasBody || method.Body.Instructions == null) continue;
                
                LogVerbose($"    Examining {method.Body.Instructions.Count} IL instructions in method {methodName}");
                foreach (var instr in method.Body.Instructions)
                {
                    if (instr.Operand is not MemberRef memberRef) continue;
                    
                    if (!IsReferenceMatch(memberRef.DeclaringType, targetRefs, out matchIndex))
                        continue;
                        
                    var matchName = targetRefs[matchIndex].ToString();
                    var debugInfo = GetDebugInfo(method, instr);
                    var message = debugInfo is null
                        ? $"Method '{typeName}.{methodName}' uses member '{memberRef.FullName}' at IL 0x{instr.Offset:X} referencing {matchName}."
                        : $"Method '{typeName}.{methodName}' uses member '{memberRef.FullName}' at {debugInfo} (IL 0x{instr.Offset:X}) referencing {matchName}.";
                    LogVerbose(message);

                    var taskItem = new TaskItem(sourcePath);
                    taskItem.SetMetadata("AssemblyReference", matchName);
                    taskItem.SetMetadata("ReferenceType", "Instruction");
                    taskItem.SetMetadata("TypeName", typeName);
                    taskItem.SetMetadata("MemberName", methodName);
                    taskItem.SetMetadata("InstructionOffset", $"0x{instr.Offset:X}");
                    taskItem.SetMetadata("ReferencedMember", memberRef.FullName);
                    taskItem.SetMetadata("Description", message);

                    if (debugInfo != null)
                    {
                        LogVerbose($"      Found source location: {debugInfo}");
                        var parts = debugInfo.Split(new[] {':', ' '}, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5)
                        {
                            taskItem.SetMetadata("DeclaringFile", parts[0]);
                            taskItem.SetMetadata("DeclaringStartLine", parts[1]);
                            taskItem.SetMetadata("DeclaringStartColumn", parts[2]);
                            taskItem.SetMetadata("DeclaringEndLine", parts[3]);
                            taskItem.SetMetadata("DeclaringEndColumn", parts[4]);
                        }
                    }

                    foundCauses.Add(taskItem);
                }
            }
        }

        if (!type.HasNestedTypes) return;

        // Process the nested types.
        LogVerbose($"  Processing {type.NestedTypes.Count} nested types in {typeName}");
        foreach (var nestedType in type.NestedTypes)
            ProcessType(nestedType, targetRefs, foundCauses, sourcePath);
    }

    private void CheckCustomAttributes(CustomAttributeCollection? customAttributes, AssemblyRefMatch[] targetRefs,
        List<ITaskItem> foundCauses, string elementName, string elementType, string sourcePath)
    {
        if (customAttributes == null) return;
        
        foreach (var attr in customAttributes)
        {
            LogVerbose($"Checking custom attribute: {attr.AttributeType.FullName}");

            if (!IsReferenceMatch(attr.AttributeType, targetRefs, out var matchIndex))
                continue;

            var matchName = targetRefs[matchIndex].ToString();
            var attributeTypeName = attr.AttributeType.FullName;
            var message =
                $"{elementType} '{elementName}' has attribute '{attributeTypeName}' referencing {matchName}.";
            LogVerbose(message);

            var parts = elementName.Split('.');
            var typeName = string.Join(".", parts.Take(parts.Length - 1));
            var memberName = parts.Length > 0 ? parts[parts.Length - 1] : "";

            AddReferenceTaskItem(
                foundCauses,
                sourcePath,
                matchName,
                typeName,
                memberName: memberName,
                referenceType: "CustomAttribute",
                customAttributeReferenceType: elementType,
                referencedType: attributeTypeName,
                message: message);
        }
    }

    private void AddReferenceTaskItem(
        List<ITaskItem> items,
        string sourcePath,
        string assemblyReference,
        string typeName,
        string memberName = "",
        string referenceType = "",
        string referencedType = "",
        string parameterName = "",
        string parameterIndex = "",
        string customAttributeReferenceType = "",
        string message = "")
    {
        LogVerbose($"Adding reference cause: {referenceType} in {typeName}.{memberName} to {assemblyReference}");
        
        var item = new TaskItem(sourcePath);

        // Parse assembly reference to extract name and version
        var assemblyNameParts = assemblyReference.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
        var assemblyName = assemblyNameParts[0].Trim();
        var assemblyVersion = assemblyNameParts.Length > 1 ? assemblyNameParts[1].Trim() : "";

        item.SetMetadata("AssemblyReference", assemblyReference);
        item.SetMetadata("AssemblyReferenceName", assemblyName);
        item.SetMetadata("AssemblyReferenceVersion", assemblyVersion);

        item.SetMetadata("ReferenceType", referenceType);
        item.SetMetadata("TypeName", typeName);

        if (!string.IsNullOrEmpty(memberName))
            item.SetMetadata("MemberName", memberName);

        if (!string.IsNullOrEmpty(referencedType))
            item.SetMetadata("ReferencedType", referencedType);

        if (!string.IsNullOrEmpty(parameterName))
            item.SetMetadata("ParameterName", parameterName);

        if (!string.IsNullOrEmpty(parameterIndex))
            item.SetMetadata("ParameterIndex", parameterIndex);

        if (!string.IsNullOrEmpty(customAttributeReferenceType))
            item.SetMetadata("CustomAttributeReferenceType", customAttributeReferenceType);

        item.SetMetadata("Description", message);

        items.Add(item);
    }

    private string? GetDebugInfo(MethodDef method, Instruction instr)
    {
        var sp = instr.SequencePoint;
        if (sp is null)
        {
            LogVerbose($"No direct sequence point for instruction at offset 0x{instr.Offset:X}, looking for closest preceding sequence point");
            // If this instruction doesn't have a sequence point,
            // find the closest preceding instruction that has one
            foreach (var i in method.Body.Instructions)
            {
                if (i.Offset >= instr.Offset)
                    break;

                if (i.SequencePoint is null)
                    continue;

                sp = i.SequencePoint;
                LogVerbose($"Found preceding sequence point at instruction offset 0x{i.Offset:X}");
                break;
            }
        }

        if (sp is null)
        {
            LogVerbose("No sequence point found for this instruction");
            return null;
        }

        var fileName = sp.Document?.Url ?? "<unknown file>";
        var line = sp.StartLine;
        var column = sp.StartColumn;
        var endLine = sp.EndLine;
        var endColumn = sp.EndColumn;
        var result = $"{fileName}:{line}:{column} to {endLine}:{endColumn}";
        LogVerbose($"Sequence point: {result}");
        return result;
    }

    private bool IsReferenceMatch(ITypeDefOrRef? type, AssemblyRefMatch[] targetRefs, out int matchIndex)
    {
        matchIndex = -1;
        if (type?.Scope is not AssemblyRef ar)
        {
            LogVerbose($"Type {type?.FullName} scope is not an AssemblyRef");
            return false;
        }

        LogVerbose($"Checking if {type.FullName} from {ar.Name}, {ar.Version} matches any target references");
        
        for (matchIndex = 0; matchIndex < targetRefs.Length; matchIndex++)
        {
            var target = targetRefs[matchIndex];
            var nuGetVersion = new NuGetVersion(ar.Version);
            
            if (!target.IsMatch(ar.Name, nuGetVersion))
                continue;
                
            LogVerbose($"Found match with target reference: {target}");
            return true;
        }

        matchIndex = -1;
        return false;
    }

    private bool IsReferenceMatch(IType? type, AssemblyRefMatch[] targetRefs, out int matchIndex)
    {
        matchIndex = -1;
        return type is ITypeDefOrRef typeDefOrRef
               && IsReferenceMatch(typeDefOrRef, targetRefs, out matchIndex);
    }
}
