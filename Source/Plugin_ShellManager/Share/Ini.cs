﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Plugin_ShellManager.Share
{
	/// <summary>
	///     StreamReader implementation, which read from an INI file.
	///     IniReader DOES NOT override any StreamReader methods. New ones are added.
	/// </summary>
	public class IniReader : StreamReader
	{
		private IniElement _current;
		public IniReader(Stream str)
			: base(str)
		{
		}

		public IniReader(Stream str, Encoding enc)
			: base(str, enc)
		{
		}

		public IniReader(string path)
			: base(path)
		{
		}

		public IniReader(string path, Encoding enc)
			: base(path, enc)
		{
		}

		public IniElement Current
		{
			get { return _current; }
		}

		public static IniElement ParseLine(string line)
		{
			if (line == null)
				return null;
			if (line.Contains("\n"))
				throw new ArgumentException("String passed to the ParseLine method cannot contain more than one line.");
			var trim = line.Trim();
			IniElement elem = null;
			if (IniBlankLine.IsLineValid(trim))
				elem = new IniBlankLine(1);
			else if (IniCommentary.IsLineValid(line))
				elem = new IniCommentary(line);
			else if (IniSectionStart.IsLineValid(trim))
				elem = new IniSectionStart(line);
			else if (IniValue.IsLineValid(trim))
				elem = new IniValue(line);
			return elem ?? new IniElement(line);
		}

		public static List<IniElement> ParseText(string text)
		{
			if (text == null)
				return null;
			var ret = new List<IniElement>();
			IniElement currEl, lastEl = null;
			var lines = text.Split(new[] { IniSettings.NewLine }, StringSplitOptions.None);
			foreach (var line in lines)
			{
				currEl = ParseLine(line);
				if (IniSettings.GroupElements)
				{
					if (lastEl != null)
					{
						if (currEl is IniBlankLine && lastEl is IniBlankLine)
						{
							((IniBlankLine)lastEl).LinesCount++;
							continue;
						}
						if (currEl is IniCommentary && lastEl is IniCommentary)
						{
							((IniCommentary)lastEl).Comment += IniSettings.NewLine + ((IniCommentary)currEl).Comment;
							continue;
						}
					}
					else
						lastEl = currEl;
				}
				lastEl = currEl;
				ret.Add(currEl);
			}
			return ret;
		}

		public IniElement ReadNextElement()
		{
			_current = ParseLine(base.ReadLine());
			return _current;
		}

		public List<IniElement> ReadElementsToEnd()
		{
			var ret = ParseText(base.ReadToEnd());
			return ret;
		}

		public IniSectionStart GotoSection(string sectionName)
		{
			while (true)
			{
				var str = ReadLine();
				if (str == null)
				{
					_current = null;
					return null;
				}
				if (IniSectionStart.IsLineValid(str))
				{
					var sect = ParseLine(str) as IniSectionStart;
					if (sect != null &&
						(sect.SectionName == sectionName ||
						(!IniSettings.CaseSensitive && sect.SectionName.ToLowerInvariant() == sectionName)))
					{
						_current = sect;
						return sect;
					}
				}
			}
		}

		public List<IniElement> ReadSection()
		{
			if (_current == null || !(_current is IniSectionStart))
				throw new InvalidOperationException("The current position of the reader must be at IniSectionStart. Use GotoSection method");
			var ret = new List<IniElement>();
			var theCurrent = _current;
			ret.Add(theCurrent);
			string text = "", temp;
			while ((temp = base.ReadLine()) != null)
			{
				if (IniSectionStart.IsLineValid(temp.Trim()))
				{
					_current = new IniSectionStart(temp);
					break;
				}
				text += temp + IniSettings.NewLine;
			}
			if (text.EndsWith(IniSettings.NewLine) && text != IniSettings.NewLine)
				text = text.Substring(0, text.Length - IniSettings.NewLine.Length);
			ret.AddRange(ParseText(text));
			return ret;
		}

		public List<IniValue> ReadSectionValues()
		{
			var elements = ReadSection();
			return elements.OfType<IniValue>().ToList();
		}

		public IniValue GotoValue(string key)
		{
			return GotoValue(key, false);
		}

		public IniValue GotoValue(string key, bool searchWholeFile)
		{
			while (true)
			{
				var str = ReadLine();
				if (str == null)
					return null;
				if (IniValue.IsLineValid(str.Trim()))
				{
					var val = ParseLine(str) as IniValue;
					if (val != null &&
						(val.Key == key || (!IniSettings.CaseSensitive && val.Key.ToLowerInvariant() == key.ToLowerInvariant())))
						return val;
				}
				if (!searchWholeFile && IniSectionStart.IsLineValid(str.Trim()))
					return null;
			}
		}
	}

	public class IniWriter : StreamWriter
	{
		public IniWriter(Stream str)
			: base(str)
		{
		}

		public IniWriter(string str)
			: base(str)
		{
		}

		public IniWriter(Stream str, Encoding enc)
			: base(str, enc)
		{
		}

		public IniWriter(string str, bool append)
			: base(str, append)
		{
		}

		public static string OutputString(Ini file)
		{
			var sb = new StringBuilder();
			foreach (var element in file.elements)
			{
				if (!IniSettings.PreserveFormatting)
					element.FormatDefault();
				// do not write if: 
				if (!( // 1) element is a blank line AND blank lines are not allowed
					(element is IniBlankLine && !IniSettings.AllowBlankLines)
					// 2) element is an empty value AND empty values are not allowed
					|| (!IniSettings.AllowEmptyValues && element is IniValue && ((IniValue)element).Value == "")))
					sb.Append(element.Line + IniSettings.NewLine);
			}
			return sb.ToString();
		}
		public void WriteElement(IniElement element)
		{
			if (!IniSettings.PreserveFormatting)
				element.FormatDefault();
			// do not write if: 
			if (!( // 1) element is a blank line AND blank lines are not allowed
				(element is IniBlankLine && !IniSettings.AllowBlankLines)
				// 2) element is an empty value AND empty values are not allowed
				|| (!IniSettings.AllowEmptyValues && element is IniValue && ((IniValue)element).Value == "")))
				base.WriteLine(element.Line);
		}

		public void WriteElements(List<IniElement> elements)
		{
			lock (elements)
				foreach (var el in elements)
					WriteElement(el);
		}

		public void WriteIniFile(Ini file)
		{
			WriteElements(file.elements);
		}

		public void WriteSection(IniSection section)
		{
			WriteElement(section.SectionStart);
			for (var i = section.Parent.elements.IndexOf(section.SectionStart) + 1; i < section.Parent.elements.Count; i++)
			{
				if (section.Parent.elements[i] is IniSectionStart)
					break;
				WriteElement(section.Parent.elements[i]);
			}
		}
	}

	public class Ini
	{
		internal List<IniElement> elements = new List<IniElement>();
		internal List<IniSection> sections = new List<IniSection>();

		public IniSection this[string sectionName]
		{
			get
			{
				var sect = GetSection(sectionName);
				if (sect != null)
					return sect;
				IniSectionStart start;
				if (sections.Count > 0)
				{
					var prev = sections[sections.Count - 1].SectionStart;
					start = prev.CreateNew(sectionName);
				}
				else
					start = IniSectionStart.FromName(sectionName);
				if (IniSettings.SeparateSection)
				{
					elements.Add(new IniBlankLine(1));
				}
				elements.Add(start);
				sect = new IniSection(this, start);
				sections.Add(sect);
				return sect;
			}
		}

		public string Header
		{
			get
			{
				if (elements.Count > 0)
					if (elements[0] is IniCommentary && !(!IniSettings.SeparateHeader
															&& elements.Count > 1 && !(elements[1] is IniBlankLine)))
						return ((IniCommentary)elements[0]).Comment;
				return "";
			}
			set
			{
				if (elements.Count > 0 && elements[0] is IniCommentary && !(!IniSettings.SeparateHeader
																				&& elements.Count > 1 && !(elements[1] is IniBlankLine)))
				{
					if (value == "")
					{
						elements.RemoveAt(0);
						if (IniSettings.SeparateHeader && elements.Count > 0 && elements[0] is IniBlankLine)
							elements.RemoveAt(0);
					}
					else
						((IniCommentary)elements[0]).Comment = value;
				}
				else if (value != "")
				{
					if ((elements.Count == 0 || !(elements[0] is IniBlankLine)) && IniSettings.SeparateHeader)
						elements.Insert(0, new IniBlankLine(1));
					elements.Insert(0, IniCommentary.FromComment(value));
				}
			}
		}

		public string Foot
		{
			get
			{
				if (elements.Count > 0)
				{
					if (elements[elements.Count - 1] is IniCommentary)
						return ((IniCommentary)elements[elements.Count - 1]).Comment;
				}
				return "";
			}
			set
			{
				if (value == "")
				{
					if (elements.Count > 0 && elements[elements.Count - 1] is IniCommentary)
					{
						elements.RemoveAt(elements.Count - 1);
						if (elements.Count > 0 && elements[elements.Count - 1] is IniBlankLine)
							elements.RemoveAt(elements.Count - 1);
					}
				}
				else
				{
					if (elements.Count > 0)
					{
						if (elements[elements.Count - 1] is IniCommentary)
							((IniCommentary)elements[elements.Count - 1]).Comment = value;
						else
							elements.Add(IniCommentary.FromComment(value));
						if (elements.Count > 2)
						{
							if (!(elements[elements.Count - 2] is IniBlankLine) && IniSettings.SeparateHeader)
								elements.Insert(elements.Count - 1, new IniBlankLine(1));
							else if (value == "")
								elements.RemoveAt(elements.Count - 2);
						}
					}
					else
						elements.Add(IniCommentary.FromComment(value));
				}
			}
		}

		private IniSection GetSection(string name)
		{
			var lower = name.ToLowerInvariant();
			for (var i = 0; i < sections.Count; i++)
				if (sections[i].Name == name || (!IniSettings.CaseSensitive && sections[i].Name.ToLowerInvariant() == lower))
					return sections[i];
			return null;
		}

		public string[] GetSectionNames()
		{
			var ret = new string[sections.Count];
			for (var i = 0; i < sections.Count; i++)
				ret[i] = sections[i].Name;
			return ret;
		}

		public static Ini FromFile(string path)
		{
			if (!File.Exists(path))
			{
				File.Create(path).Close();
				return new Ini();
			}
			var reader = new IniReader(path);
			var ret = FromStream(reader);
			reader.Close();
			return ret;
		}

		public static Ini FromElements(IEnumerable<IniElement> elemes)
		{
			var ret = new Ini();
			ret.elements.AddRange(elemes);
			if (ret.elements.Count > 0)
			{
				IniSection section = null;

				if (ret.elements[ret.elements.Count - 1] is IniBlankLine)
					ret.elements.RemoveAt(ret.elements.Count - 1);
				foreach (var element in ret.elements)
				{
					var el = element;
					if (el is IniSectionStart)
					{
						section = new IniSection(ret, (IniSectionStart)el);
						ret.sections.Add(section);
					}
					else if (section != null)
						section.Elements.Add(el);
					else if (ret.sections.Exists(a => a.Name == ""))
						ret.sections[0].Elements.Add(el);
					else if (el is IniValue)
					{
						section = new IniSection(ret, IniSectionStart.FromName(""));
						section.Elements.Add(el);
						ret.sections.Add(section);
					}
				}
			}
			return ret;
		}

		public static Ini FromStream(IniReader reader)
		{
			return FromElements(reader.ReadElementsToEnd());
		}

		public static Ini FromString(string str)
		{
			return FromElements(IniReader.ParseText(str));
		}

		public void Save(string path)
		{
			var writer = new IniWriter(path);
			Save(writer);
			writer.Close();
		}

		public void Save(IniWriter writer)
		{
			writer.WriteIniFile(this);
		}

		public string OutputString()
		{
			return IniWriter.OutputString(this);
		}

		public void DeleteSection(string name)
		{
			var section = GetSection(name);
			if (section == null)
				return;
			var sect = section.SectionStart;
			elements.Remove(sect);
			for (var i = elements.IndexOf(sect) + 1; i < elements.Count; i++)
			{
				if (elements[i] is IniSectionStart)
					break;
				elements.RemoveAt(i);
			}
		}

		/// <summary>Formats whole INI file.</summary>
		/// <param name="preserveIntendation">If true, old intendation will be standarized but not removed.</param>
		public void Format(bool preserveIntendation)
		{
			var lastSectIntend = "";
			var lastValIntend = "";
			for (var i = 0; i < elements.Count; i++)
			{
				var el = elements[i];
				if (preserveIntendation)
				{
					if (el is IniSectionStart)
						lastValIntend = lastSectIntend = el.Indentation;
					else if (el is IniValue)
						lastValIntend = el.Indentation;
				}
				el.FormatDefault();
				if (preserveIntendation)
				{
					if (el is IniSectionStart)
						el.Indentation = lastSectIntend;
					else if (el is IniCommentary && i != elements.Count - 1 && !(elements[i + 1] is IniBlankLine))
						el.Indentation = elements[i + 1].Indentation;
					else
						el.Indentation = lastValIntend;
				}
			}
		}

		/// <summary>Joins sections which are definied more than one time.</summary>
		public void UnifySections()
		{
			var dict = new Dictionary<string, int>();
			int index;
			for (var i = 0; i < sections.Count; i++)
			{
				var sect = sections[i];
				if (dict.ContainsKey(sect.Name))
				{
					index = dict[sect.Name] + 1;
					elements.Remove(sect.SectionStart);
					sections.Remove(sect);
					for (var j = sect.Elements.Count - 1; j >= 0; j--)
					{
						var el = sect.Elements[j];
						if (!(j == sect.Elements.Count - 1 && el is IniCommentary))
							elements.Remove(el);
						if (!(el is IniBlankLine))
						{
							elements.Insert(index, el);
							var val = this[sect.Name].FirstValue();
							if (val != null)
								el.Indentation = val.Indentation;
							else
								el.Indentation = this[sect.Name].SectionStart.Indentation;
						}
					}
				}
				else
					dict.Add(sect.Name, elements.IndexOf(sect.SectionStart));
			}
		}
	}

	public class IniSection
	{
		public List<IniElement> Elements = new List<IniElement>();
		public Ini Parent;
		public IniSectionStart SectionStart;

		internal IniSection(Ini parent, IniSectionStart sect)
		{
			SectionStart = sect;
			Parent = parent;
		}

		public string Name
		{
			get { return SectionStart.SectionName; }
			set { SectionStart.SectionName = value; }
		}

		public string Comment
		{
			get { return Name == "" ? "" : GetComment(SectionStart); }
			set
			{
				if (Name != "")
					SetComment(SectionStart, value);
			}
		}

		public void InsertComment(string comment)
		{
			var com = IniCommentary.FromComment(comment);
			Parent.elements.Add(com);
		}

		public string InlineComment
		{
			get { return SectionStart.InlineComment; }
			set { SectionStart.InlineComment = value; }
		}

		public string this[string key]
		{
			get
			{
				var v = GetValue(key);
				return v == null ? null : v.Value;
			}
			set
			{
				var v = GetValue(key);
				if (v != null)
				{
					v.Value = value;
					return;
				}
				SetValue(key, value);
			}
		}

		public string this[string key, string defaultValue]
		{
			get
			{
				var val = this[key];
				if (string.IsNullOrEmpty(val))
					return defaultValue;
				return val;
			}
			set { this[key] = value; }
		}

		private void SetComment(IniElement el, string comment)
		{
			var index = Parent.elements.IndexOf(el);
			if (IniSettings.CommentChars.Length == 0)
				throw new NotSupportedException("Comments are currently disabled. Setup ConfigFileSettings.CommentChars property to enable them.");
			IniCommentary com;
			if (index > 0 && Parent.elements[index - 1] is IniCommentary)
			{
				com = ((IniCommentary)Parent.elements[index - 1]);
				if (comment == "")
					Parent.elements.Remove(com);
				else
				{
					com.Comment = comment;
					com.Indentation = el.Indentation;
				}
			}
			else if (comment != "")
			{
				com = IniCommentary.FromComment(comment);
				com.Indentation = el.Indentation;
				Parent.elements.Insert(index, com);
			}
		}

		private string GetComment(IniElement el)
		{
			var index = Parent.elements.IndexOf(el);
			if (index != 0 && Parent.elements[index - 1] is IniCommentary)
				return ((IniCommentary)Parent.elements[index - 1]).Comment;
			return "";
		}

		private IniValue GetValue(string key)
		{
			var lower = key.ToLowerInvariant();
			for (var i = 0; i < Elements.Count; i++)
				if (Elements[i] is IniValue)
				{
					var val = (IniValue)Elements[i];
					if (val.Key == key || (!IniSettings.CaseSensitive && val.Key.ToLowerInvariant() == lower))
						return val;
				}
			return null;
		}

		public void SetComment(string key, string comment)
		{
			var val = GetValue(key);
			if (val == null) return;
			SetComment(val, comment);
		}

		public void SetInlineComment(string key, string comment)
		{
			var val = GetValue(key);
			if (val == null) return;
			val.InlineComment = comment;
		}

		public string GetInlineComment(string key)
		{
			var val = GetValue(key);
			return val == null ? null : val.InlineComment;
		}

		public string GetComment(string key)
		{
			var val = GetValue(key);
			return val == null ? null : GetComment(val);
		}

		public void RenameKey(string key, string newName)
		{
			var v = GetValue(key);
			if (key == null) return;
			v.Key = newName;
		}

		public void DeleteKey(string key)
		{
			var v = GetValue(key);
			if (key == null) return;
			Parent.elements.Remove(v);
			Elements.Remove(v);
		}

		private void SetValue(string key, string value)
		{
			IniValue ret = null;
			IniValue prev = LastValue();

			if (IniSettings.PreserveFormatting)
			{
				if (prev != null && prev.Indentation.Length >= SectionStart.Indentation.Length)
					ret = prev.CreateNew(key, value);
				else
				{
					IniElement el;
					var valFound = false;
					for (var i = Parent.elements.IndexOf(SectionStart) - 1; i >= 0; i--)
					{
						el = Parent.elements[i];
						if (el is IniValue)
						{
							ret = ((IniValue)el).CreateNew(key, value);
							valFound = true;
							break;
						}
					}
					if (!valFound)
						ret = IniValue.FromData(key, value);
					if (ret.Indentation.Length < SectionStart.Indentation.Length)
						ret.Indentation = SectionStart.Indentation;
				}
			}
			else
				ret = IniValue.FromData(key, value);
			if (prev == null)
			{
				Elements.Insert(Elements.IndexOf(SectionStart) + 1, ret);
				Parent.elements.Insert(Parent.elements.IndexOf(SectionStart) + 1, ret);
			}
			else
			{
				Elements.Insert(Elements.IndexOf(prev) + 1, ret);
				Parent.elements.Insert(Parent.elements.IndexOf(prev) + 1, ret);
			}
		}

		internal IniValue LastValue()
		{
			for (var i = Elements.Count - 1; i >= 0; i--)
			{
				if (Elements[i] is IniValue)
					return (IniValue)Elements[i];
			}
			return null;
		}

		internal IniValue FirstValue()
		{
			return Elements.OfType<IniValue>().FirstOrDefault();
		}

		public ReadOnlyCollection<string> GetKeys()
		{
			var list = new List<string>(Elements.Count);
			list.AddRange(Elements.OfType<IniValue>().Select(t => (t).Key));
			return new ReadOnlyCollection<string>(list);
		}

		public override string ToString()
		{
			return SectionStart + " (" + Elements.Count + " elements)";
		}

		public void Format(bool preserveIntendation)
		{
			foreach (var element in Elements)
			{
				var el = element;
				var lastIntend = el.Indentation;
				el.FormatDefault();
				if (preserveIntendation)
					el.Indentation = lastIntend;
			}
		}
	}
}