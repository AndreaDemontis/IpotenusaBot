﻿using System;
using System.Linq;
using System.Data;

namespace Ipotenusa.Utils
{
	public class Formatting
	{
		/// <summary>
		/// Truncate the string by adding at the end "...".
		/// </summary>
		/// <param name="value">String.</param>
		/// <param name="maxChars">Number of characters.</param>
		public static string Truncate(string value, int maxChars)
		{
			return value.Length <= maxChars ? value : (value.Substring(0, maxChars) + "...");
		}

		/// <summary>
		/// Makes a line with underscores.
		/// </summary>
		/// <param name="len">Line length</param>
		public static string Separator(int len, bool bold)
		{
			string b = "";
			for (; len > 0; --len)
				b += "\\_";
			return bold ? "**" + b + "**" : b;
		}

		/// <summary>
		/// Adds tags for text code.
		/// </summary>
		/// <param name="text">Text to view as code.</param>
		/// <returns>Formatted text.</returns>
		public static string Code(string text)
		{
			return $"```\n{text}```";
		}


		public static string BarChart(int width, int value)
		{
			value = Math.Max(Math.Min(value, 100), 0);

			string compose = $"%{value.ToString("D2")} ";

			int fill = (int)(((double)value / 100) * width);

			for (int i = 0; i < width; ++i)
			{
				if (i < fill)
					compose += "█";
				else compose += "░";
			}

			compose += "";

			return compose;
		}

		/// <summary>
		/// Makes an ascii table using a datatable.
		/// </summary>
		/// <param name="table">Data to insert.</param>
		/// <returns>An ascii string with the data table.</returns>
		public static string MakeTable(DataTable table)
		{
			int[] columnsWidth = new int[table.Columns.Count];

			for (int i = 0; i < table.Columns.Count; ++i)
			{
				columnsWidth[i] = table.Columns[i].ColumnName.Length;
				foreach (DataRow row in table.Rows)
				{
					try
					{
						columnsWidth[i] = Math.Max(columnsWidth[i], row.Field<string>(i).Length);
					}
					catch (Exception) { }
				}

				columnsWidth[i] += 2;
			}

			string builder = "";
			
			{
				for (int i = 0; i < table.Columns.Count; ++i)
				{
					string val = table.Columns[i].ColumnName;
					val += new String(' ', columnsWidth[i] - val.Length - 2);
					builder += $"| {val} ";
				}

				builder += "|\n ";
				builder += new string('-', columnsWidth.Sum() + columnsWidth.Length - 1);
				builder += " \n";

				foreach (DataRow row in table.Rows)
				{
					for (int i = 0; i < table.Columns.Count; ++i)
					{
						string val = row.Field<string>(i);

						if (val == null)
							val = "";

						val += new String(' ', columnsWidth[i] - val.Length - 2);
						builder += $"| {val} ";
					}

					builder += "|\n ";
					builder += new string('-', columnsWidth.Sum() + columnsWidth.Length - 1);
					builder += " \n";
				}
			}

			return "```\n" + builder + "\n```";
		}
	}
}
