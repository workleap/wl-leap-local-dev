// Adapted from https://github.com/Azure/azure-storage-net/blob/v11.2.3/Lib/Common/NameValidator.cs
// Copyright 2013 Microsoft Corporation
// Licensed under the Apache License, Version 2.0
//
// See also:
// - https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-name-rules#microsoftstorage
// - https://learn.microsoft.com/en-us/rest/api/storageservices/naming-and-referencing-containers--blobs--and-metadata#container-names

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Leap.Cli.Dependencies.Azurite;

internal static class AzuriteNameValidator
{
    private const string ResourceNameEmptyErrorFormat = "Invalid {0} name '{1}'. The {0} name may not be null, empty, or whitespace only.";
    private const string InvalidResourceNameLengthErrorFormat = "Invalid {0} name '{1}'. The {0} name must be between {2} and {3} characters long.";
    private const string InvalidResourceNameErrorFormat = "Invalid {0} name '{1}'. Check MSDN for more information about valid {0} naming.";

    private const int ContainerShareQueueTableMinLength = 3;
    private const int ContainerShareQueueTableMaxLength = 63;

    [SuppressMessage("Performance", "CA1802:Use literals where appropriate", Justification = "Cannot access a static enum in a non-static context.")]
    private static readonly RegexOptions RegexOptions = RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant;

    private static readonly Regex ShareContainerQueueRegex = new Regex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions);
    private static readonly Regex TableRegex = new Regex("^[A-Za-z][A-Za-z0-9]*$", RegexOptions);
    private static readonly Regex MetricsTableRegex = new Regex(@"^\$Metrics(HourPrimary|MinutePrimary|HourSecondary|MinuteSecondary)?(Transactions)(Blob|Queue|Table)$", RegexOptions);

    public static void ValidateContainerName(string containerName)
    {
        if (!("$root".Equals(containerName, StringComparison.Ordinal) || "$logs".Equals(containerName, StringComparison.Ordinal)))
        {
            ValidateShareContainerQueueHelper(containerName, "container");
        }
    }

    public static void ValidateQueueName(string queueName)
    {
        ValidateShareContainerQueueHelper(queueName, "queue");
    }

    private static void ValidateShareContainerQueueHelper(string resourceName, string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, ResourceNameEmptyErrorFormat, resourceType, resourceName));
        }

        if (resourceName.Length is < ContainerShareQueueTableMinLength or > ContainerShareQueueTableMaxLength)
        {
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, InvalidResourceNameLengthErrorFormat, resourceType, resourceName, ContainerShareQueueTableMinLength, ContainerShareQueueTableMaxLength));
        }

        if (!ShareContainerQueueRegex.IsMatch(resourceName))
        {
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, InvalidResourceNameErrorFormat, resourceType, resourceName));
        }
    }

    public static void ValidateTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, ResourceNameEmptyErrorFormat, "table", tableName));
        }

        if (tableName.Length is < ContainerShareQueueTableMinLength or > ContainerShareQueueTableMaxLength)
        {
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, InvalidResourceNameLengthErrorFormat, "table", tableName, ContainerShareQueueTableMinLength, ContainerShareQueueTableMaxLength));
        }

        if (!(TableRegex.IsMatch(tableName) || MetricsTableRegex.IsMatch(tableName) || tableName.Equals("$MetricsCapacityBlob", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, InvalidResourceNameErrorFormat, "table", tableName));
        }
    }
}