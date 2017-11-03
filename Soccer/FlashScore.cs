using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium.PhantomJS;
using System.Collections.ObjectModel;
using OpenQA.Selenium;
using System.Text.RegularExpressions;
using System.Threading;

namespace Soccer
{
    public class FlashScore
    {
        private PhantomJSDriver JSDriver = null;
        private ReadOnlyCollection<IWebElement> MainPageMatchesList = null;

        private List<FlashScore_MatchData> List_MatchData = new List<FlashScore_MatchData>();

        private const int NO_ODD_ON_FLASHSCORE = -2;
        //private PhantomJSDriver JSDriver_ForPage = null;
        //private IReadOnlyCollection<IWebElement> OddPage;

        public FlashScore()
        {

        }


        public void Init()
        {
            StartPhantomServer();
            if (MainPageMatchesList == null)
            {
                MainPageMatchesList = GetMatchesList("http://flashscore.com");
                foreach (IWebElement singleMatch in MainPageMatchesList)
                {
                    string nameToAdd = singleMatch.Text;
                    string link;
                    nameToAdd = ReplaceShortString(nameToAdd);
                    nameToAdd = nameToAdd.Replace("\r\n-\r\n", "\r\n");
                    nameToAdd = nameToAdd.Replace("\r", "");

                    link = singleMatch.GetAttribute("id").Split('_')[2];

                    List_MatchData.Add(new FlashScore_MatchData { Name = nameToAdd, Link = link });
                }
            }
        }

        private void StartPhantomServer()
        {
            if (JSDriver == null)
            {
                JSDriver = new PhantomJSDriver();
            }
        }

        private ReadOnlyCollection<IWebElement> GetMatchesList(string url)
        {
            ReadOnlyCollection<IWebElement> matchesNotStarted = null;
            JSDriver.Url = url;
            JSDriver.Navigate();

            string content = JSDriver.PageSource;
            matchesNotStarted = JSDriver.FindElementsByClassName("stage-scheduled"); // only get matches that already started
            return matchesNotStarted;
        }

        public float GetOddForMatch(string matchName)
        {
            float matchOdd = -1;
            string oddUrlForMatch = GetLinkForMatch(matchName);
            if (oddUrlForMatch != null)
            {
                string matchUrlForOdds = "http://www.flashscore.com/match/" + oddUrlForMatch + "/#odds-comparison;over-under;full-time";
                IWebElement currentOddCell;
                string currentOddCellString = null;
                JSDriver.Url = matchUrlForOdds;
                JSDriver.Navigate();

                string content = JSDriver.PageSource;
                ReadOnlyCollection<IWebElement> matchOdds = JSDriver.FindElementsById("odds_ou_1.5");
                if (matchOdds.Count > 0)
                {
                    currentOddCell = matchOdds[0];
                    currentOddCellString = currentOddCell.Text;
                    Regex regex = new Regex(@"([0-9]{1}).([0-9]{2})(\r)");
                    Match regex_match = regex.Match(currentOddCellString);
                    if (regex_match.Success)
                    {
                        matchOdd = float.Parse(regex_match.Value);
                    }
                }
                else
                {
                    matchOdd = NO_ODD_ON_FLASHSCORE;
                }
            }
            else if (oddUrlForMatch == "statelException")
            {
                return -3;
            }
            return matchOdd;
        }

        /* public string GetLinkForMatch(string matchName)
         {
             string aux = null;

             string homeTeam = matchName.Split(new string[] { " vs " }, StringSplitOptions.None)[0];
             string awayTeam = matchName.Split(new string[] { " vs " }, StringSplitOptions.None)[1];

             homeTeam = ReplaceShortString(homeTeam);    //homeTeam.Replace("Standard", "St.");
             awayTeam = ReplaceShortString(awayTeam);    //awayTeam.Replace("Standard", "St.");

             string homeTeamFromFlash = null, awayTeamFromFlash = null;

             string foundMatchLink = null;
             foreach (IWebElement singleMatch in MainPageMatchesList)
             {
                 if (singleMatch != null)
                 {


                     //match.Text:
                     //  "22:00  \r\nLille\r\nMarseille"
                     //  "20:30  \r\nJusto Jose de Urquiza\r\n-\r\nDock Sud\r\n   "
                     try
                     {
                         aux = singleMatch.Text.Replace("\r\n-\r\n", "\r\n");
                         aux = aux.Replace("\r", "");
                     }
                     catch(StaleElementReferenceException e)
                     {
                         //Console.WriteLine(e.Message);
                         return "statelException";
                         //break;
                     }

                     homeTeamFromFlash = aux.Split('\n')[1];
                     awayTeamFromFlash = aux.Split('\n')[2];

                     if (homeTeam.Contains(homeTeamFromFlash) || homeTeamFromFlash.Contains(homeTeam)) // home team found
                     {
                         if (awayTeam.Contains(awayTeamFromFlash) || awayTeamFromFlash.Contains(awayTeam)) // awayTeam Found
                         {
                             foundMatchLink = singleMatch.GetAttribute("id").Split('_')[2];
                             break;
                         }
                     }
                 }
             }
             return foundMatchLink;
         }*/


        public string GetLinkForMatch(string matchName)
        {
            string aux = null;
            string foundMatchLink = null;
            string homeTeam = matchName.Split(new string[] { " vs " }, StringSplitOptions.None)[0];
            string awayTeam = matchName.Split(new string[] { " vs " }, StringSplitOptions.None)[1];

            homeTeam = ReplaceShortString(homeTeam);    //homeTeam.Replace("Standard", "St.");
            awayTeam = ReplaceShortString(awayTeam);    //awayTeam.Replace("Standard", "St.");

            foreach (FlashScore_MatchData data in List_MatchData)
            {
                string homeTeamFromFlash = null, awayTeamFromFlash = null;
                homeTeamFromFlash = data.Name.Split('\n')[1];
                awayTeamFromFlash = data.Name.Split('\n')[2];

                if (homeTeam.Contains(homeTeamFromFlash) || homeTeamFromFlash.Contains(homeTeam)) // home team found
                {
                    if (awayTeam.Contains(awayTeamFromFlash) || awayTeamFromFlash.Contains(awayTeam)) // awayTeam Found
                    {
                        foundMatchLink = data.Link;
                        break;
                    }
                }
            }
            return foundMatchLink;
        }

        private string ReplaceShortString(string stringToEdit)
        {
            string stringToReturn = stringToEdit;

            try
            {
                stringToReturn = stringToReturn.Replace("Standard", "Std.");
                stringToReturn = stringToReturn.Replace("Cent.", "Central");
                stringToReturn = stringToReturn.Replace("Stuttgart B", "Stuttgart II");
                stringToReturn = stringToReturn.Replace("Kick.", "Kickers");
                stringToReturn = stringToReturn.Replace("Walld.", "Walldorf");
                //stringToReturn = stringToReturn.Replace("Hessen Kassel", "Kassel");
                stringToReturn = stringToReturn.Replace("Al ", "Al-");
                //stringToReturn = stringToReturn.Replace("Graffin Vlasim ", "Vlasim");
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERROR] exception: " + e.Message);
            }



            return stringToReturn;
        }

        public void CloseDriver()
        {
            if (JSDriver != null)
            {
                JSDriver.Quit();
                JSDriver.Dispose();
            }
        }

        public bool IsServerRunning()
        {
            return JSDriver == null ? false : true;
        }

        private string FindURLByName(string url)
        {
            return null;
        }

        public float GetMatchOdd(string matchName)
        {
            return 0;
        }
    }
}
