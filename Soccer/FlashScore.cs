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
        private ReadOnlyCollection<IWebElement> Matches = null;

        //private PhantomJSDriver JSDriver_ForPage = null;
        //private IReadOnlyCollection<IWebElement> OddPage;

        public FlashScore()
        {

        }


        public void Init()
        {
            StartPhantomServer();
            if (Matches == null)
            {
                Matches = GetMatchesList("http://flashscore.com");
            }
        }

        private void StartPhantomServer()
        {
            if(JSDriver == null)
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
                currentOddCell = matchOdds[0];
                currentOddCellString = currentOddCell.Text;
                Regex regex = new Regex(@"([0-9]{1}).([0-9]{2})(\r)");
                Match regex_match = regex.Match(currentOddCellString);
                if (regex_match.Success)
                {
                    matchOdd = float.Parse(regex_match.Value);
                }
            }
            return matchOdd;
        }

        public string GetLinkForMatch(string matchName)
        {
            string aux = null;

            string homeTeam = matchName.Split(new string[] { " vs " }, StringSplitOptions.None)[0];
            string awayTeam = matchName.Split(new string[] { " vs " }, StringSplitOptions.None)[1];

            homeTeam = homeTeam.Replace("Standard", "St.");
            awayTeam = awayTeam.Replace("Standard", "St.");

            string homeTeamFromFlash = null, awayTeamFromFlash = null;

            string foundMatchLink = null;
            foreach (IWebElement match in Matches)
            {
                if (match != null)
                {
                    //match.Text:
                    //  "22:00  \r\nLille\r\nMarseille"
                    //  "20:30  \r\nJusto Jose de Urquiza\r\n-\r\nDock Sud\r\n   "
                    try
                    {
                        aux = match.Text.Replace("\r\n-\r\n", "\r\n");
                        aux = aux.Replace("\r", "");
                    }
                    catch(Exception e)
                    {
                        break;
                    }
                    homeTeamFromFlash = aux.Split('\n')[1];
                    awayTeamFromFlash = aux.Split('\n')[2];

                    if (homeTeam.Contains(homeTeamFromFlash) || homeTeamFromFlash.Contains(homeTeam)) // home team found
                    {
                        if (awayTeam.Contains(awayTeamFromFlash) || awayTeamFromFlash.Contains(awayTeam)) // awayTeam Found
                        {
                            foundMatchLink = match.GetAttribute("id").Split('_')[2];
                            break;
                        }
                    }
                }
            }
            return foundMatchLink;
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
