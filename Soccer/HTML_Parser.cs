using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using System.Net.Http;
using System.Threading;
using System.Windows.Threading;
using System.Web;
using System.IO;

namespace Soccer
{
    public struct PageDataStruct
    {
        public List<string> MatchName;
        public List<string> MatchLink;

        public string[] ScoredAndConceded;
    };

    public struct MatchDataStruct
    {
        public string MatchName;
        public string MatchLink;
        public float[] Scored_and_Conceded;
        public float Average1_and_4;
        public float[] CleanSheets;
        public float Cota;
    };


    public class HTML_Parser
    {
        private WebClient MainPageWeb;
        private string MainPageContent;

        private PageDataStruct PageData;
        private List<MatchDataStruct> MatchData;
        private FlashScore flashScore;

        private const int A = 0;
        private const int B = 1;
        private const int C = 2;
        private const int D = 3;

        // private const int _MATCHES_LIMIT_ = 100;

        public HTML_Parser(string url)
        {
            PageData = new PageDataStruct();

            MatchData = new List<MatchDataStruct>();

            PageData.ScoredAndConceded = new string[4];
            InitializeStructure();

            Thread getLinksThread = new Thread(() => GetLinksFromPage(url));
            getLinksThread.Start();

        }

        private void InitializeStructure()
        {
            PageData.MatchLink = new List<string>();
            PageData.MatchName = new List<string>();
        }

        private async void GetLinksFromPage(string url)
        {
            string link = null;
            HttpClient client = new HttpClient();
            byte[] pageResponse = await client.GetByteArrayAsync(url);

            string source = Encoding.GetEncoding("utf-8").GetString(pageResponse, 0, pageResponse.Length - 1);
            source = WebUtility.HtmlDecode(source);

            HtmlDocument resultat = new HtmlDocument();
            resultat.LoadHtml(source);

            List<HtmlNode> tofTitle = resultat.DocumentNode.Descendants().Where(
                    x => (
                        x.Name == "tr" &&
                        x.Attributes["class"] != null &&
                        x.Attributes["class"].Value.Contains("trow8"))).ToList();



            for (int i = 0; i < tofTitle.Count; i++)
            {
                List<HtmlNode> td = tofTitle[i].Descendants("td").ToList();

                foreach (HtmlNode item in td)
                {
                    try
                    {
                        List<HtmlNode> foundNode = item.Descendants("a").ToList();
                        if (foundNode.Count > 0)
                        {
                            link = foundNode[0].GetAttributeValue("href", null);
                            if (link.Contains("pmatch.asp?"))
                            {
                                PageData.MatchLink.Add(link);
                                //PageData.MatchName.Add(item.InnerText);
                                Console.WriteLine("link: " + link);
                                PushStringToQueue(link);

                            }
                        }
                    }
                    catch(Exception e)
                    {
                    }
                }
            }

            await MainWindow.main.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                MainWindow.main.Label_FindingMatches.Content = "Finished";
                MainWindow.main.FlyOut_FindingMatches.Visibility = System.Windows.Visibility.Collapsed;
                //MainWindow.main.Label_LinksFound.Content = PageData.MatchLink.Count.ToString();
            }));
        }

        public void InitializeJSDriver()
        {
            flashScore = new FlashScore();
        }

        public async void ProcessMatchesPage(string url)
        {
            // process each page individually
            string STR_matchUrl = null;
            string STR_matchName = null;
            float[] STR_scoredAndConceded = new float[4];
            float STR_average1and4 = 0;
            float[] STR_cleanSheets = new float[2];
            float STR_cota = 0;


            MatchDataStruct matchToAdd = new MatchDataStruct();
            if (url != null)
            {

                STR_matchUrl = GetEncodedURL("http://soccerstats.com/" + url);
                string pageContent = await GetPageContent(url);
                if (pageContent != null)
                {
                    STR_scoredAndConceded = Get_ScoredAndConceded(pageContent);
                    STR_average1and4 = Get_Average1And4(pageContent);
                    STR_cleanSheets = Get_CleanSheets(pageContent);
                    STR_matchName = Get_MatchName(pageContent);
                }

                if (IsMatchWorthBetting(STR_scoredAndConceded, STR_average1and4, STR_cleanSheets))
                {
                    matchToAdd.MatchLink = STR_matchUrl;
                    matchToAdd.Scored_and_Conceded = STR_scoredAndConceded;
                    matchToAdd.Average1_and_4 = STR_average1and4;
                    matchToAdd.CleanSheets = STR_cleanSheets;
                    matchToAdd.MatchName = STR_matchName;
                    //get odd for the specific match
                    STR_cota = flashScore.GetOddForMatch(STR_matchName);
                    matchToAdd.Cota = STR_cota;

                    MatchData.Add(matchToAdd);
                    MainWindow.main.GoodMatchesQueue.Enqueue(matchToAdd);
                }
            }
        }

        private string GetEncodedURL(string encodedUrl)
        {
            try
            {
                using (StringWriter decodedString = new StringWriter())
                {
                    HttpUtility.HtmlDecode(encodedUrl, decodedString);
                    return decodedString.ToString();
                }
            }
            catch(Exception e)
            {
                return "Caught exception: " + e.Message;
            }
        }

        private bool IsMatchWorthBetting(float[] scoredAndConceded, float average1And4, float[] cleanSheets)
        {
            if (scoredAndConceded[0] >= 2 && scoredAndConceded[1] >= 2 && scoredAndConceded[2] >= 2 && scoredAndConceded[3] >= 2 &&
                average1And4 >= 1.5 &&
                cleanSheets[0] <= 20 && cleanSheets[1] <= 20)
                return true;
            return false;
        }

        private float[] Get_ScoredAndConceded(string pageContent)
        {
            float[] goals = new float[4];
            int index = 0;

            Regex regex = new Regex(@"Goals conceded per match(?s)(.*)Matches over 1.5 goals");
            foreach(Match match in regex.Matches(pageContent))
            {
                Regex regex_Number = new Regex(@"([0-9]{1})\.[0-9]*");
                foreach(Match match_Number in regex_Number.Matches(match.Value))
                {
                    if(index >= 2 && index <= 5)
                    {
                        //tbd if condition for if (float.Parse(match_Number.Value) >= 2)
                        goals[index - 2] = float.Parse(match_Number.Value);
                    }
                    index++;
                }
            }

            return goals;
        }

        private float Get_Average1And4(string pageContent)
        {

            float averageGoals = 0;

            Regex regex = new Regex(@"average \(1\) & \(4\) values(?s)(.*)average \(2\) & \(3\) values");
            Match match = regex.Match(pageContent);
            if(match.Success)
            {
                Regex regex_Number = new Regex(@"([0-9]{1})\.[0-9]*");
                Match match_Number = regex_Number.Match(match.Value);
                if(match_Number.Success)
                {
                    averageGoals = float.Parse(match_Number.Value);
                }
            }
            return averageGoals;
        }

        private float[] Get_CleanSheets(string pageContent)
        {
            float[] cleanSheets = new float[2];
            int index = 0;

            Regex regex = new Regex(@"NO GOAL(?s)(.*)Won-to-nil");
            Match match = regex.Match(pageContent);
            if(match.Success)
            {
                Regex regex_Numbers = new Regex(@"([0-9]{1,})\%<");
                foreach(Match match_Number in regex_Numbers.Matches(match.Value))
                {
                    if(index == 0 || index == 3)
                    {
                        cleanSheets[(index * 3) % 8] = float.Parse(match_Number.Value.Replace("%<", ""));
                    }
                    index++;
                }
            }
            return cleanSheets;
        }

        private string  Get_MatchName(string pageContent)
        {
            string title = null;

            Regex regex = new Regex(@"<title>(?s)(.*) - team");
            Match match = regex.Match(pageContent);
            if(match.Success)
            {
                title = match.Value.Replace("<title>", "");
                title = title.Replace(" - team", "");


            }
            return title;
        }

        private async Task<string> GetPageContent(string pageURL)
        {
            string stringToReturn = null;
            byte[] bytes = Encoding.Default.GetBytes(pageURL);
            string encodedLink = "http://soccerstats.com/" + Encoding.UTF8.GetString(bytes);
            HttpClient httpClient = new HttpClient();
            string sourceDecode = null;
            HtmlDocument resultatDoc = new HtmlDocument();

            byte[] pageResponse = null;
            try
            {
                pageResponse = await httpClient.GetByteArrayAsync(encodedLink);

                sourceDecode = Encoding.GetEncoding("utf-8").GetString(pageResponse, 0, pageResponse.Length - 1);
                sourceDecode = WebUtility.HtmlDecode(sourceDecode);
                resultatDoc.LoadHtml(sourceDecode);
                stringToReturn = resultatDoc.DocumentNode.OuterHtml;
            }
            catch
            {
                stringToReturn = null;
            }
            return stringToReturn;
        }

        public void CloseDriver()
        {
            flashScore.CloseDriver();
        }

        private void PushStringToQueue(string stringToSend)
        {
            MainWindow.main.LinksQueue.Enqueue(stringToSend);
        }

        public List<string> GetLinks
        {
            get
            {
               return PageData.MatchLink;
            }
        }

        public PageDataStruct GetLinksStruct
        {
            get
            {
                return this.PageData;
            }
        }


    }
}
