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
using System.Media;
using System.Reflection;

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
        public int MatchType;
    };


    public class HTML_Parser
    {
        //private WebClient MainPageWeb;
        //private string MainPageContent;

        private PageDataStruct PageData;
        private List<MatchDataStruct> MatchData;
        private FlashScore flashScore = null;

        private const int A = 0;
        private const int B = 1;
        private const int C = 2;
        private const int D = 3;
        private const int _NORMAL_ = 4;
        private const int _LEAGUE_ = 5;

        // private const int _MATCHES_LIMIT_ = 100;

        public HTML_Parser()
        {
            PageData = new PageDataStruct();

            MatchData = new List<MatchDataStruct>();

            PageData.ScoredAndConceded = new string[4];


            InitializeStructure();
        }

        public void InitParser(string url)
        {

            Thread getLinksThread = new Thread(() => GetLinksFromPage(url));
            getLinksThread.Start();

            if (MainWindow.main.Toggle_ParseOdds.IsChecked == true)
            {
                if (flashScore == null)
                {
                    flashScore = new FlashScore();
                }
            }
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

            List<HtmlNode> tofTitleList = resultat.DocumentNode.Descendants().Where(
                    x => (
                        x.Name == "tr" &&
                        x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("trow8"))).ToList();


            for (int i = 0; i < tofTitleList.Count; i++)
            {
                List<HtmlNode> td = tofTitleList[i].Descendants("td").ToList();

                foreach (HtmlNode item in td)
                {
                    try
                    {
                        List<HtmlNode> foundNode = item.Descendants("a").ToList();
                        if (foundNode.Count > 0)
                        {
                            link = foundNode[0].GetAttributeValue("href", null);
                            if (link.Contains("pmatch.asp?") || link.Contains("leagueview_team.asp?"))
                            {
                                PageData.MatchLink.Add(link);
                                //PageData.MatchName.Add(item.InnerText);
                                //Console.WriteLine("link: " + link);
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
                MainWindow.main.FlyOut_FindingMatches.Visibility = System.Windows.Visibility.Collapsed;

                string x = (Assembly.GetEntryAssembly().Location + "");
                x = x.Replace("Soccer.exe", @"sounds\sound.wav");
                x = x.Replace(@"\bin\Debug", "");
                SoundPlayer player = new SoundPlayer();
                player.SoundLocation = x;
                player.Play();
                //MainWindow.main.Label_LinksFound.Content = PageData.MatchLink.Count.ToString();
            }));
        }

        public void InitializeJSDriver()
        {
            //flashScore = new FlashScore();
            flashScore.Init();
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
            int STR_Type;


            MatchDataStruct matchToAdd = new MatchDataStruct();
            if (url != null)
            {

                if (url.Contains("leagueview_team.asp?"))
                {
                    STR_Type = _LEAGUE_;
                }
                else
                {
                    STR_Type = _NORMAL_;
                }

                STR_matchUrl = GetEncodedURL("http://soccerstats.com/" + url);
                string pageContent = await GetPageContent(url);
                if (pageContent != null)
                {
                    STR_scoredAndConceded = Get_ScoredAndConceded(pageContent, STR_Type);
                    STR_average1and4 = Get_Average1And4(pageContent);
                    STR_cleanSheets = Get_CleanSheets(pageContent, STR_Type);
                    STR_matchName = Get_MatchName(pageContent, STR_Type);
                }

                //if (IsMatchWorthBetting(STR_scoredAndConceded, STR_average1and4, STR_cleanSheets, STR_Type))
                var x = MainWindow.main.NeedCompute();
                if((MainWindow.main.NeedCompute() == false) || (IsMatchWorthBetting(STR_scoredAndConceded, STR_average1and4, STR_cleanSheets, STR_Type)))
                {
                    matchToAdd.MatchLink = STR_matchUrl;
                    matchToAdd.Scored_and_Conceded = STR_scoredAndConceded;
                    matchToAdd.Average1_and_4 = STR_average1and4;
                    matchToAdd.CleanSheets = STR_cleanSheets;
                    matchToAdd.MatchName = STR_matchName;
                    //get odd for the specific match


                    if (GetParseToggleState() && STR_Type == _NORMAL_)
                    {
                        STR_cota = flashScore.GetOddForMatch(STR_matchName);
                    }
                    else
                    {
                        STR_cota = -1;
                    }

                    matchToAdd.Cota = STR_cota;

                    MatchData.Add(matchToAdd);
                    MainWindow.main.GoodMatchesQueue.Enqueue(matchToAdd);
                }
            }
        }

        private bool GetParseToggleState()
        {
            return MainWindow.main.ParseOdds;
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

        private bool IsMatchWorthBetting(float[] scoredAndConceded, float average1And4, float[] cleanSheets, int type)
        {
            if (type == _NORMAL_)
            {
                if (scoredAndConceded[0] >= 2 && scoredAndConceded[1] >= 2 && scoredAndConceded[2] >= 2 && scoredAndConceded[3] >= 2 &&
                    average1And4 >= 1.5 &&
                    cleanSheets[0] <= 20 && cleanSheets[1] <= 20)
                    return true;
            }
            else if(type == _LEAGUE_)
            {
                if (scoredAndConceded[0] >= 2 && scoredAndConceded[1] >= 2 && scoredAndConceded[2] >= 2 &&
                    cleanSheets[0] <= 20 && cleanSheets[1] <= 20)
                    return true;
            }
            return false;
        }

        private float[] Get_ScoredAndConceded(string pageContent, int type)
        {
            Match  matchToDo;
            
            float[] goals = new float[4];
            int index = 0;
            //regex.Match(pageContent).Groups[1]
            Regex regex = new Regex(@"Goals conceded per match(?s)(.*)Matches over 1.5 goals");
            foreach(Match match in regex.Matches(pageContent))
            {
                matchToDo = match;
                if (type == _NORMAL_)
                {
                    Regex regex_Number = new Regex(@"([0-9]{1})\.[0-9]*");
                    foreach (Match match_Number in regex_Number.Matches(matchToDo.Value))
                    {
                        if (index >= 2 && index <= 5)
                        {
                            //tbd if condition for if (float.Parse(match_Number.Value) >= 2)
                            goals[index - 2] = float.Parse(match_Number.Value);
                        }
                        index++;
                    }
                }
                else if(type == _LEAGUE_)
                {

                    string string2 = regex.Match(pageContent).Groups[1].Value; // parse again
                    regex = new Regex(@"Goals conceded per match(?s)(.*)");
                    matchToDo = regex.Match(string2);

                    Regex regex_Number = new Regex(@"([0-9]{1})\.[0-9]*");
                    foreach (Match match_Number in regex_Number.Matches(matchToDo.Value))
                    {
                        //tbd if condition for if (float.Parse(match_Number.Value) >= 2)
                        if (index >= 3)
                        {
                            goals[index - 3] = float.Parse(match_Number.Value);
                        }
                        index++;
                    }
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

        private float[] Get_CleanSheets(string pageContent, int type)
        {
            float[] cleanSheets = new float[2];
            int index = 0;
            if (type == _NORMAL_)
            {

                Regex regex = new Regex(@"NO GOAL(?s)(.*)Won-to-nil");
                Match match = regex.Match(pageContent);
                if (match.Success)
                {
                    Regex regex_Numbers = new Regex(@"([0-9]{1,})\%<");
                    foreach (Match match_Number in regex_Numbers.Matches(match.Value))
                    {
                        if (index == 0 || index == 3)
                        {
                            cleanSheets[(index * 3) % 8] = float.Parse(match_Number.Value.Replace("%<", ""));
                        }
                        index++;
                    }
                }
            }
            else
            {
                Regex regex = new Regex(@"Clean sheets(?s)(.*)Won-to-nil");
                Match match = regex.Match(pageContent);
                if (match.Success)
                {

                    string string2 = match.Groups[1].Value;
                    regex = new Regex(@"Clean sheets(?s)(.*)");
                    match = regex.Match(string2);


                    Regex regex_Numbers = new Regex(@"([0-9]{1,})\%<");
                    foreach (Match match_Number in regex_Numbers.Matches(match.Value))
                    {
                        if (index < 2)
                        {
                            cleanSheets[index] = float.Parse(match_Number.Value.Replace("%<", ""));
                        }
                        index++;
                    }
                }
            }
            return cleanSheets;
        }

        private string  Get_MatchName(string pageContent, int type)
        {
            string title = null;
            if (type == _NORMAL_)
            {
                Regex regex = new Regex(@"<title>(?s)(.*) - team");
                Match match = regex.Match(pageContent);
                if (match.Success)
                {
                    title = match.Value.Replace("<title>", "");
                    title = title.Replace(" - team", "");


                }
            }
            else
            {
                Regex regex = new Regex(@"<title>(?s)(.*)\</title>");
                Match match = regex.Match(pageContent);
                if (match.Success)
                {
                    title = match.Value.Replace("<title>Champions League results, stats and scores - stats and results, ", "");
                    title = title.Replace("team ", "");
                    title = title.Replace("</title>", "");
                    string[] splitTeams = title.Split('-');
                    title = splitTeams[0] + "vs " + splitTeams[1];
                }
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
            if (flashScore != null)
            {
                flashScore.CloseDriver();
            }
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
