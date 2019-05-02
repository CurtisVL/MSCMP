using System;
using System.Collections;
using System.IO;


namespace MSCMP.Utilities
{
	internal class HtmlWriter 
		: IDisposable
	{
		private readonly StreamWriter _streamWriter;

		private readonly string _fileName = "";
		public string FileName => _fileName;

		public HtmlWriter(string fileName)
		{
			_fileName = fileName;
			_streamWriter = new StreamWriter(fileName, false, System.Text.Encoding.UTF8);
			if (_streamWriter == null)
			{
				Logger.Log($"Failed to write document - {fileName}!");
			}
			else
			{
				_streamWriter.AutoFlush = false;
			}
		}

		public void Dispose()
		{
			_streamWriter.Close();
		}

		public void WriteString(string str)
		{
			_streamWriter.Write(str);
		}

		private readonly Stack _tagStack = new Stack();

		public void ShortTag(string name, string attributes = "")
		{
			WriteString($"<{name}");
			if (attributes.Length > 0)
			{
				WriteString($" {attributes}");
			}
			WriteString("/>");
		}

		public void StartTag(string name, string attributes = "")
		{
			WriteString($"<{name}");
			if (attributes.Length > 0)
			{
				WriteString($" {attributes}");
			}
			WriteString(">");
			_tagStack.Push(name);
		}

		public void EndTag()
		{
			string tagName = (string)_tagStack.Pop();
			WriteString($"</{tagName}>");
		}

		public void NewLine()
		{
			ShortTag("br");
		}

		public void WriteValue(string value)
		{
			WriteString(value);
		}

		public void OneLiner(string tag, string value, string attributes = "")
		{
			StartTag(tag, attributes);
			WriteValue(value);
			EndTag();
		}

		public void Link(string url, string value, string attributes = "")
		{
			StartTag("a", $"href=\"{url}\" " + attributes);
			WriteValue(value);
			EndTag();
		}
		public delegate void WriteContents(HtmlWriter writer);

		public static bool WriteDocument(string fileName, string title, string cssStyleFile, WriteContents writeDelegate)
		{
			using (HtmlWriter writer = new HtmlWriter(fileName))
			{
				writer.StartTag("head");
				{
					writer.StartTag("title");
					{
						writer.WriteValue(title);
					}
					writer.EndTag();

					if (cssStyleFile.Length > 0)
					{
						writer.ShortTag("link", "rel=\"stylesheet\" type=\"text/css\" href=\"" + cssStyleFile + "\"");
					}
				}
				writer.EndTag();

				writer.StartTag("body");
				{
					writeDelegate(writer);
				}
				writer.EndTag();
			}
			return true;
		}
	}
}
