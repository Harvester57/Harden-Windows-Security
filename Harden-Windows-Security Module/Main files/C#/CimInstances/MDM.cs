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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

/// root\cimv2\mdm is the namespace for CSPs
/// https://learn.microsoft.com/en-us/windows/win32/wmisdk/common-information-model
namespace HardenWindowsSecurity;

/// <summary>
/// Class that deals with MDM/CSPs/Intune
/// </summary>
internal static class MDM
{
	/// <summary>
	/// Gets the results of all of the Intune policies from the system
	/// </summary>
	/// <returns></returns>
	internal static Dictionary<string, List<Dictionary<string, object>>> Get()
	{
		// Running the asynchronous method synchronously and returning the result
		return Task.Run(GetAsync).GetAwaiter().GetResult();
	}

	// Asynchronous method to get the results
	private static async Task<Dictionary<string, List<Dictionary<string, object>>>> GetAsync()
	{
		// Set the location of the CSV file containing the MDM list
		string csvFilePath = Path.Combine(GlobalVars.path, "Resources", "MDMResultClasses.csv");

		// Create a dictionary where keys are the class names and values are lists of dictionaries
		Dictionary<string, List<Dictionary<string, object>>> results = [];

		try
		{
			// Read class names and namespaces from CSV file asynchronously
			List<MdmRecord> records = await ReadCsvFileAsync(csvFilePath);

			// Create a list of tasks for querying each class
			List<Task> tasks = [];

			// Iterate through records
			foreach (MdmRecord record in records)
			{
				// Process only authorized records
				if (record.Authorized.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
				{

					// Add a new task for each class query
					tasks.Add(Task.Run(() =>
					{
						// List to store results for the current class
						List<Dictionary<string, object>> classResults = [];

						// Create management scope object
						ManagementScope scope = new(record.Namespace);
						// Connect to the WMI namespace
						try
						{
							scope.Connect();
						}
						catch (ManagementException e)
						{
							// Write verbose error message if connection fails
							Logger.LogMessage($"Error connecting to namespace {record.Namespace}: {e.Message}", LogTypeIntel.Error);
						}

						// Create object query for the current class
						string classQuery = record.Class.Trim();
						ObjectQuery query = new("SELECT * FROM " + classQuery);

						// Create management object searcher for the query
						using ManagementObjectSearcher searcher = new(scope, query);

						try
						{
							// Execute the query and iterate through the results
							foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
							{
								// Dictionary to store properties of the current class instance
								Dictionary<string, object> classInstance = [];

								// Iterate through properties of the current object
								foreach (PropertyData prop in obj.Properties)
								{
									// Store property name and its value
									classInstance[prop.Name] = GetPropertyOriginalValue(prop);
								}

								// Add class instance to results
								classResults.Add(classInstance);
							}
						}
						catch (ManagementException e)
						{
							// Write verbose error message if query fails
							Logger.LogMessage($"Error querying {record.Class}: {e.Message}", LogTypeIntel.Error);
						}

						// Add class results to main results dictionary in a thread-safe manner
						lock (results)
						{
							results[record.Class] = classResults;
						}
					}));
				}
			}

			// Wait for all tasks to complete
			await Task.WhenAll(tasks);
		}
		catch (IOException ex)
		{
			// Throw exception with error message if reading CSV file fails
			throw new InvalidOperationException($"Error reading CSV file: {ex.Message}");
		}

		// Return dictionary containing results for each class
		return results;
	}


	/// <summary>
	/// Helper method to get property value as original type
	/// </summary>
	/// <param name="prop"></param>
	/// <returns></returns>
	private static object GetPropertyOriginalValue(PropertyData prop)
	{
		// Return the value of the property
		return prop.Value;
	}


	/// <summary>
	/// Helper method to read CSV file asynchronously
	/// </summary>
	/// <param name="filePath"></param>
	/// <returns></returns>
	private static async Task<List<MdmRecord>> ReadCsvFileAsync(string filePath)
	{
		List<MdmRecord> records = [];

		using (StreamReader reader = new(filePath))
		{
			string? line; // Explicitly declare line as nullable
			bool isFirstLine = true;
			while ((line = await reader.ReadLineAsync()) is not null)
			{
				if (isFirstLine)
				{
					isFirstLine = false;
					continue; // Skip the header line
				}

				if (line is null)
				{
					continue;
				}

				string[] values = line.Split(',');

				// because of using "Comment" column in the CSV file optionally for certain MDM CIMs
				if (values.Length >= 3)
				{
					records.Add(new MdmRecord
					{
						Namespace = values[0].Trim(),
						Class = values[1].Trim(),
						Authorized = values[2].Trim()
					});
				}
			}
		}

		return records;
	}


	// Class to represent a record in the CSV file
	private sealed class MdmRecord
	{
		internal required string Namespace { get; set; }
		internal required string Class { get; set; }
		internal required string Authorized { get; set; }
	}
}
