using HtmlAgilityPack;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Reflection;
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

        public bool ParseOdds = true;
        //private bool AutoStart = false;

        //public FlashScore FlashScore;

        public MainWindow()
        {
            main = this;
            InitializeComponent();
            {
                DropDown_MatchDay.Items.Add("Monday");
                DropDown_MatchDay.Items.Add("Tuesday");
                DropDown_MatchDay.Items.Add("Wednesday");
                DropDown_MatchDay.Items.Add("Thursday");
                DropDown_MatchDay.Items.Add("Friday");
                DropDown_MatchDay.Items.Add("Saturday");
                DropDown_MatchDay.Items.Add("Sunday");
            }

            //this.FlashScore = new FlashScore();

            Thread UIThread = new Thread(UpdateUI_Thread);
            UIThread.Start();

            Thread GoodMatchesUIThread = new Thread(UpdateGoodMatchesUI);
            GoodMatchesUIThread.Start();

            Parser = new HTML_Parser();


        }

        public void UpdateUI_Thread()
        {
            for(; ;)
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
                                Cota = receivingStruct.Cota == -1 ? "N/A" : 
                                (
                                    (
                                        receivingStruct.Cota == -2 ? ("No odd") : 
                                        (
                                            receivingStruct.Cota == -3 ? ("Exc") : receivingStruct.Cota.ToString()
                                        )
                                    )
                                ),
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



        private void FinishedLoadingMatches(object sender, DoWorkEventArgs e)
        {

        }
        private async void AccessPage(string url)
        {
            //Console.WriteLine("Accessing page " + url + "\n -> " + c);
            //string link = null;

            byte[] bytes = Encoding.Default.GetBytes(url);

            HttpClient client = new HttpClient();
            try
            {
                byte[] pageResponse = await client.GetByteArrayAsync("http://soccerstats.com/" + Encoding.UTF8.GetString(bytes));

                string source = Encoding.GetEncoding("utf-8").GetString(pageResponse, 0, pageResponse.Length - 1);
                source = WebUtility.HtmlDecode(source);

                HtmlDocument resultat = new HtmlDocument();
                resultat.LoadHtml(source);
                //GetDataFromPage(resultat.DocumentNode.OuterHtml, Encoding.UTF8.GetString(bytes));
            }
            catch { }
        }

        /*
             Goals conceded per match
            </td><td width='13%' align='center'>1.20 </td><td width='13%' align='center'><b>1.75</b> <font color='red' size='1'>(4)</font></td></tr>
            <tr class='trow2'><td width='13%' align='center' height='22'><font color='blue' size='1'>(A)</font> <b>2.67</b></td><td width='13%' align='center'><font color='blue' size='1'>(B)</font> 3.20</td><td width='48%' align='center'>
            Scored+conc. per match
        */

        private int GetDayIDFromName(string day)
        {
            switch (day)
            {
                case "Monday":
                    return 5;
                case "Tuesday":
                    return 2;
                case "Wednesday":
                    return 3;
                case "Thursday":
                    return 4;
                case "Friday":
                    return 6;
                case "Saturday":
                    return 7;
                case "Sunday":
                    return 1;
                default:
                    return -1;
            }
        }

        /* ============================================================================================================================ */
        private void Button_Start_Click(object sender, RoutedEventArgs e)
        {
            PageDataStruct fullStruct = Parser.GetLinksStruct;
            List<string> linksLust = fullStruct.MatchLink;

            if (ParseOdds)
            {
                Parser.InitializeJSDriver();
            }

            FlyOut_FindingMatches.Header = "Finding matches to bet, please wait...";
            FlyOut_FindingMatches.Visibility = Visibility.Visible;
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

                string x = (Assembly.GetEntryAssembly().Location + "");
                x = x.Replace("Soccer.exe", @"sounds\sound.wav");
                x = x.Replace(@"\bin\Debug", "");
                SoundPlayer player = new SoundPlayer();
                player.SoundLocation = x;
                player.Play();
            };

            bw.RunWorkerAsync();

            //t.Start();
        }


        private void Button_GetTodaysMatches_Click(object sender, RoutedEventArgs e)
        {
            FlyOut_FindingMatches.Header = "Getting Today's matches, please wait...";
            FlyOut_FindingMatches.Visibility = Visibility.Visible;
            //Parser = new HTML_Parser("http://www.soccerstats.com/matches.asp");
            Parser.InitParser("http://www.soccerstats.com/matches.asp");

            /*if(AutoStart)
            {
                Button_Start_Click(sender, e);
            }*/
        }

        private async void Button_GetTomorrowsMatches_Click(object sender, RoutedEventArgs e)
        {

            if(DropDown_MatchDay.SelectedIndex >= 0)
            {
                FlyOut_FindingMatches.Header = "Getting " + DropDown_MatchDay.SelectedValue + " matches, please wait...";
                FlyOut_FindingMatches.Visibility = Visibility.Visible;
                //Parser = new HTML_Parser("http://www.soccerstats.com/matches.asp?matchday=2");
                Parser.InitParser("http://www.soccerstats.com/matches.asp?matchday=" + GetDayIDFromName(DropDown_MatchDay.SelectedValue.ToString()));

                /*if (AutoStart)
                {
                    Button_Start_Click(sender, e);
                }*/
            }
            else
            {
                await this.ShowMessageAsync("Invalid Match Day", "Please select a match day!");
            }

        }

        private void MetroWindow_Closing(object sender, CancelEventArgs e)
        {
            if (Parser != null)
            {
                Parser.CloseDriver();
            }
            System.Environment.Exit(1);

        }

        private void Toggle_ParseOdds_IsCheckedChanged(object sender, EventArgs e)
        {
            ParseOdds = Toggle_ParseOdds.IsChecked.Value;
        }

        private void Toggle_AutoStart_IsCheckedChanged(object sender, EventArgs e)
        {
            //AutoStart = Toggle_AutoStart.IsChecked.Value;
        }
    }
}
