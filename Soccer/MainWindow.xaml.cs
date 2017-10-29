﻿using HtmlAgilityPack;
using MahApps.Metro.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Soccer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private HTML_Parser Parser;
        public static MainWindow main;
        private List<double[]> GoalsList = new List<double[]>();
        private List<string> AverageValues = new List<string>();
        private List<string[]> CleanSheets = new List<string[]>();

        public ConcurrentQueue<string> LinksQueue = new ConcurrentQueue<string>();
        ObservableCollection<DataObject> List = new ObservableCollection<DataObject>();

        public ConcurrentQueue<MatchDataStruct> GoodMatchesQueue = new ConcurrentQueue<MatchDataStruct>();
        private ObservableCollection<MatchedMatches> GoodMatchesList = new ObservableCollection<MatchedMatches>();

        public MainWindow()
        {
            main = this;
            InitializeComponent();


            Thread UIThread = new Thread(UpdateUI_Thread);
            UIThread.Start();

            Thread GoodMatchesUIThread = new Thread(UpdateGoodMatchesUI);
            GoodMatchesUIThread.Start();


        }

        public void UpdateUI_Thread()
        {
            while(true)
            {
                if(LinksQueue.Count > 0)
                {
                    if (LinksQueue.TryDequeue(out string receivingStruct))
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
                        {
                            //Label_LinksFound.Content = receivingStruct.MatchLink.Count.ToString();
                            List.Add(new DataObject()
                            {
                                MatchLink = receivingStruct,
                                //MatchName = "aaaaaaaaaaa"
                            });
                            this.DataGrid_MatchesGrid.ItemsSource = List;
                            Label_LinksFound.Content = this.DataGrid_MatchesGrid.Items.Count;
                            Button_Start.IsEnabled = true;
                        }));
                    }
                }
            }
        }

        public void UpdateGoodMatchesUI()
        {
            for(; ; )
            {
                if(GoodMatchesQueue.Count > 0)
                {
                    if(GoodMatchesQueue.TryDequeue(out MatchDataStruct receivingStruct))
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            GoodMatchesList.Add(new MatchedMatches()
                            {
                                Name = receivingStruct.MatchName,
                                Link = receivingStruct.MatchLink,
                                ScoredAndConceded_A = receivingStruct.Scored_and_Conceded[0],
                                ScoredAndConceded_B = receivingStruct.Scored_and_Conceded[1],
                                ScoredAndConceded_C = receivingStruct.Scored_and_Conceded[2],
                                ScoredAndConceded_D = receivingStruct.Scored_and_Conceded[3],
                                Average = receivingStruct.Average1_and_4,
                                CleanSheets_Home = receivingStruct.CleanSheets[0],
                                CleanSheets_Away = receivingStruct.CleanSheets[1]
                            });

                            this.DataGrid_GoodMatches.ItemsSource = GoodMatchesList;
                        }));
                    }
                }
            }
        }

        private void Button_Start_Click(object sender, RoutedEventArgs e)
        {
            FlyOut_FindingMatches.Header = "Finding matches to bet, please wait...";
            FlyOut_FindingMatches.Visibility = Visibility.Visible;
            PageDataStruct fullStruct = Parser.GetLinksStruct;
            List<string> linksLust = fullStruct.MatchLink;

            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (callbackSender, callbackEvent) =>
            {
                foreach (string lnk in linksLust)
                {
                    if (lnk != null)
                    {
                        //Thread th = new Thread(() => AccessPage(lnk));
                        //Parser.ProcessMatchesPage(lnk);
                        Thread th = new Thread(() => Parser.ProcessMatchesPage(lnk));
                        th.Start();
                        Thread.Sleep(200);
                    }
                }
            };

            bw.RunWorkerCompleted += (sender2, e2) =>
            {
                FlyOut_FindingMatches.Visibility = Visibility.Collapsed;
            };

            bw.RunWorkerAsync();

            //t.Start();
        }

        private void FinishedLoadingMatches(object sender, DoWorkEventArgs e)
        {

        }
        int c = 0;
        private async void AccessPage(string url)
        {
            //Console.WriteLine("Accessing page " + url + "\n -> " + c);
            c++;
            string link = null;

            byte[] bytes = Encoding.Default.GetBytes(url);

            HttpClient client = new HttpClient();
            try
            {
                byte[] pageResponse = await client.GetByteArrayAsync("http://soccerstats.com/" + Encoding.UTF8.GetString(bytes));

                string source = Encoding.GetEncoding("utf-8").GetString(pageResponse, 0, pageResponse.Length - 1);
                source = WebUtility.HtmlDecode(source);

                HtmlDocument resultat = new HtmlDocument();
                resultat.LoadHtml(source);
                GetDataFromPage(resultat.DocumentNode.OuterHtml, Encoding.UTF8.GetString(bytes));
            }
            catch { }
        }

        /*
             Goals conceded per match
            </td><td width='13%' align='center'>1.20 </td><td width='13%' align='center'><b>1.75</b> <font color='red' size='1'>(4)</font></td></tr>
            <tr class='trow2'><td width='13%' align='center' height='22'><font color='blue' size='1'>(A)</font> <b>2.67</b></td><td width='13%' align='center'><font color='blue' size='1'>(B)</font> 3.20</td><td width='48%' align='center'>
            Scored+conc. per match
        */
        private void GetDataFromPage(string page, string matchLink)
        {
            /*Regex regex = new Regex(@"Goals conceded per match(?s)(.*)Matches over 1.5 goals");
            string matchedString = null;
            foreach(Match match in regex.Matches(page))
            {
                double[] goalsLocal = new double[4];
                int i = 0;
                bool brked = false;

                matchedString = match.Value;
                Regex regex2 = new Regex(@"([0-9]{1})\.[0-9]*");
                Console.Write(matchLink + ": ");
                foreach (Match match2 in regex2.Matches(matchedString))
                {
                    if  (i >= 2 && i <= 5)
                    {
                        if (double.Parse(match2.Value) >= 2)
                        {
                            Console.Write(match2.Value + ", ");
                            goalsLocal[i - 2] = float.Parse(match2.Value);
                        }
                        else
                        {
                            brked = true;
                            break;
                        }
                    }
                    i++;
                }

                if(!brked)
                {
                    Regex regex3 = new Regex(@"average \(1\) & \(4\) values(?s)(.*)average \(2\) & \(3\) values");
                    Match match3 = regex3.Match(page);
                    if (match3.Success)
                    {

                        Regex regex4 = new Regex(@"([0-9]{1})\.[0-9]*");
                        Match match4 = regex4.Match(match3.Value);
                        if (match4.Success)
                        {
                            if (float.Parse(match4.Value) >= 1.5)
                            {
                                AverageValues.Add(match4.Value);
                            }
                            else
                            {
                                brked = true;
                            }
                        }
                    }
                }
                int j = 0;
                string[] cleans = new string[2];
                if(!brked)
                {
                    Regex regex5 = new Regex(@"NO GOAL(?s)(.*)Won-to-nil");
                    Match match5 = regex5.Match(page);
                    if (match5.Success)
                    {

                        Regex regex6 = new Regex(@"([0-9]{1,})\%<");
                        //Match match6 = regex6.Match(match5.Value);
                        foreach(Match match6 in regex6.Matches(match5.Value))
                        {
                            if (j == 0 || j == 3)
                            {
                                cleans[(j * 3) % 8] = match6.Value.Replace("%<", "");
                            }
                            j++;
                        }
                    }
                }
                if(float.Parse(cleans[0]) <= 20 && float.Parse(cleans[1]) <= 20)
                {
                    CleanSheets.Add(cleans);
                    //MessageBox.Show("Match found: \n" + matchLink
                    Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(
                        () =>
                        {
                            TextBox_MatchingMatches.Text += "http://soccerstats.com/" + matchLink + "\n";
                        }));
                }
                Console.WriteLine("");
                GoalsList.Add(goalsLocal);
                
            }*/
        }

        private void Button_GetTodaysMatches_Click(object sender, RoutedEventArgs e)
        {
            FlyOut_FindingMatches.Header = "Getting Today's matches, please wait...";
            FlyOut_FindingMatches.Visibility = Visibility.Visible;
            Parser = new HTML_Parser("http://www.soccerstats.com/matches.asp");
        }

        private void Button_GetTomorrowsMatches_Click(object sender, RoutedEventArgs e)
        {
            FlyOut_FindingMatches.Header = "Getting Tomorrow's matches, please wait...";
            FlyOut_FindingMatches.Visibility = Visibility.Visible;
            Parser = new HTML_Parser("http://www.soccerstats.com/matches.asp?matchday=2");
        }

        private void MetroWindow_Closing(object sender, CancelEventArgs e)
        {
            //System.Environment.Exit(1);
        }

        /* private void Button_ClearTables_Click(object sender, RoutedEventArgs e)
         {
             DataGrid_MatchesGrid.ItemsSource = null;
             foreach (DataObject elementToRemove in DataGrid_MatchesGrid.Items)
             {
                 DataGrid_MatchesGrid.Items.Remove(elementToRemove);
             }

             DataGrid_MatchesGrid.Items.Clear();
             DataGrid_GoodMatches.ItemsSource = null;
             foreach (DataObject elementToRemove in DataGrid_GoodMatches.Items)
             {
                 DataGrid_GoodMatches.Items.Remove(elementToRemove);
             }
             DataGrid_GoodMatches.Items.Clear();


             Label_LinksFound.Content = "0";
         }*/
    }
}