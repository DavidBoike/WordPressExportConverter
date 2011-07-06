using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Text.RegularExpressions;

namespace WordPressExportConverter
{
	class Program
	{
		static void Main(string[] args)
		{
			string inpath = @"C:\Users\Dave\Desktop\wordpress.input.xml";
			string outpath = @"C:\Users\Dave\Desktop\wordpress.output.xml";
			ConvertWordpressExport(inpath, outpath);
		}

		private static void ConvertWordpressExport(string inpath, string outpath)
		{
			XmlDocument doc = new XmlDocument();
			doc.Load(inpath);

			XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
			nsmgr.AddNamespace("content", "http://purl.org/rss/1.0/modules/content/");

			XmlNodeList nodes = doc.SelectNodes("/rss/channel/item/content:encoded", nsmgr);

			foreach (XmlNode n in nodes)
			{
				string newText = ProcessBlogPost(n.InnerText);
				n.InnerText = null;
				n.AppendChild(doc.CreateCDataSection(newText));
			}

			doc.Save(outpath);

			Console.WriteLine("Done");
			Console.ReadLine();
		}

		private static Regex findImgTag = new Regex("<img", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static Regex findEndImg = new Regex("/>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		
		private static string ProcessBlogPost(string blogPost)
		{
			StringBuilder output = new StringBuilder();
			int pos = 0;
			while (true)
			{
				Match startImg = findImgTag.Match(blogPost, pos);
				if (!startImg.Success)
				{
					output.Append(blogPost.Substring(pos));
					break;
				}
				else
				{
					output.Append(blogPost.Substring(pos, startImg.Index - pos));
					Match endImg = findEndImg.Match(blogPost, startImg.Index);
					pos = endImg.Index + endImg.Length;
					string imgTag = blogPost.Substring(startImg.Index, pos - startImg.Index);

					ImgTagProcessor p = new ImgTagProcessor(imgTag);
					output.Append(p.Process());
				}
			}
			return output.ToString();
		}

		class ImgTagProcessor
		{
			static Regex findAtts = new Regex(@"(?<Att>\w+)=""(?<Value>[^""]*)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
			static Regex queryW = new Regex(@"w=(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
			static Regex queryH = new Regex(@"h=(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

			string imgTag;
			string width;
			string height;


			internal ImgTagProcessor(string imgTag)
			{
				this.imgTag = imgTag;
			}

			internal string Process()
			{
				// Extract width and height info
				foreach (Match m in findAtts.Matches(imgTag))
				{
					switch (m.Groups["Att"].Value)
					{
						case "width":
							this.width = m.Groups["Value"].Value;
							break;
						case "height":
							this.height = m.Groups["Value"].Value;
							break;
						case "src":
							Uri uri = new Uri(m.Groups["Value"].Value);
							string query = uri.Query;
							if (!String.IsNullOrEmpty(query))
							{
								Match matchW = queryW.Match(query);
								Match matchH = queryH.Match(query);
								if (matchW.Success)
									width = matchW.Groups[1].Value;
								if (matchH.Success)
									height = matchH.Groups[1].Value;
							}
							break;
					}
				}
				return findAtts.Replace(imgTag, new MatchEvaluator(EvaluateAttributeMatch));
			}

			string EvaluateAttributeMatch(Match m)
			{
				switch (m.Groups["Att"].Value)
				{
					case "src":
						UriBuilder uri = new UriBuilder(m.Groups["Value"].Value);
						List<string> queryItems = new List<string>();
						if (width != null)
							queryItems.Add("w=" + width);
						if (height != null)
							queryItems.Add("h=" + height);
						uri.Query = String.Join("&", queryItems.ToArray());
						return "src=\"" + uri.ToString() + "\"";
					default:
						return m.Value;
				}
			}
		}
	}
}
