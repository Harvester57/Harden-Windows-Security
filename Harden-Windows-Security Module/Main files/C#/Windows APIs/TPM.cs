// MIT License
//
// Copyright (c) 2023-Present - Violet Hansen - (aka HotCakeX on GitHub) - Email Address: spynetgirl@outlook.com
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// See here for more information: https://github.com/HotCakeX/Harden-Windows-Security/blob/main/LICENSE
//

using System;
using System.Globalization;
using System.Linq;
using System.Management;

namespace HardenWindowsSecurity;

/// <summary>
/// Class that contains the results of TPM status checks
/// </summary>
public sealed class TpmResult
{
	public bool IsEnabled { get; set; }
	public bool IsActivated { get; set; }
	public bool IsSrkAuthCompatible { get; set; }
	public string? ErrorMessage { get; set; }
}

public static partial class TpmStatus
{
	// Method to use the Windows APIs to check if the TPM is enabled and activated
	public static TpmResult Get()
	{
		bool isEnabled = false;
		bool isActivated = false;
		string? errorMessage = null;

		// Call TpmIsEnabled and check result
		uint result = NativeMethods.TpmIsEnabled(out byte isEnabledByte);
		if (result == 0)
		{
			isEnabled = isEnabledByte != 0;
		}
		else
		{
			errorMessage = $"{result}";
		}

		// Call TpmIsActivated and check result
		result = NativeMethods.TpmIsActivated(out byte isActivatedByte);
		if (result == 0)
		{
			isActivated = isActivatedByte != 0;
		}
		else
		{
			errorMessage = $"{result}";
		}

		return new TpmResult { IsEnabled = isEnabled, IsActivated = isActivated, ErrorMessage = errorMessage };
	}


	/// <summary>
	/// Checks TPM status by invoking WMI methods to determine if it's enabled and activated.
	/// </summary>
	/// <returns>A TpmResult containing the TPM status and any error messages encountered.</returns>
	public static TpmResult GetV2()
	{
		// Create an instance of the TpmResult class to later populate it with data
		TpmResult result = new()
		{
			// Initially set them to false so when an error occurs and method is returned, they won't be accidentally set to null or true
			IsEnabled = false,
			IsActivated = false,
			IsSrkAuthCompatible = false
		};

		try
		{
			// Query WMI to get the Win32_Tpm instance.
			using ManagementObjectSearcher searcher = new(@"root\CIMV2\Security\MicrosoftTpm", "SELECT * FROM Win32_Tpm");

			ManagementObjectCollection tpmObjects = searcher.Get();

			// If no TPM object is found, return an error message.
			if (tpmObjects.Count == 0)
			{
				result.ErrorMessage = "TPM WMI object could not be created";
				return result;
			}

			// Get the first instance of the TPM.
			ManagementObject? tpmObject = tpmObjects.OfType<ManagementObject>().FirstOrDefault();

			if (tpmObject is null)
			{
				result.ErrorMessage = "TPM instance not found";
				return result;
			}

			// Call the IsEnabled method
			ManagementBaseObject isEnabledResult = tpmObject.InvokeMethod("IsEnabled", null, null);

			if (Convert.ToUInt32(isEnabledResult["ReturnValue"], CultureInfo.InvariantCulture) != 0)
			{
				result.ErrorMessage = $"Error checking TPM enabled status: HRESULT {isEnabledResult["ReturnValue"]}";
				return result;
			}

			result.IsEnabled = Convert.ToBoolean(isEnabledResult["IsEnabled"], CultureInfo.InvariantCulture);

			// Call the IsActivated method
			ManagementBaseObject isActivatedResult = tpmObject.InvokeMethod("IsActivated", null, null);

			if (Convert.ToUInt32(isActivatedResult["ReturnValue"], CultureInfo.InvariantCulture) != 0)
			{
				result.ErrorMessage = $"Error checking TPM activation status: HRESULT {isActivatedResult["ReturnValue"]}";
				return result;
			}

			result.IsActivated = Convert.ToBoolean(isActivatedResult["IsActivated"], CultureInfo.InvariantCulture);

			// Call the IsSrkAuthCompatible method
			ManagementBaseObject IsSrkAuthCompatibleResult = tpmObject.InvokeMethod("IsSrkAuthCompatible", null, null);

			if (Convert.ToUInt32(IsSrkAuthCompatibleResult["ReturnValue"], CultureInfo.InvariantCulture) != 0)
			{
				HResultHelper.HandleHresultAndLog(Convert.ToUInt32(IsSrkAuthCompatibleResult["ReturnValue"], CultureInfo.InvariantCulture));

				return result;
			}

			result.IsSrkAuthCompatible = Convert.ToBoolean(IsSrkAuthCompatibleResult["IsSrkAuthCompatible"], CultureInfo.InvariantCulture);

		}
		catch (Exception ex)
		{
			result.ErrorMessage = $"Exception occurred: {ex.Message}";
		}

		return result;
	}
}
