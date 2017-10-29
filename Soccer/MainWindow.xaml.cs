using HtmlAgilityPack;
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

        //public FlashScore FlashScore;

        public MainWindow()
        {
            main = this;
            InitializeComponent();

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
                                Cota = receivingStruct.Cota == -1 ? "N/A" : receivingStruct.Cota.ToString(),
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


        private void Button_Start_Click(object sender, RoutedEventArgs e)
        {
            PageDataStruct fullStruct = Parser.GetLinksStruct;
            List<string> linksLust = fullStruct.MatchLink;

            Parser.InitializeJSDriver();

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
        }

        private void Button_GetTomorrowsMatches_Click(object sender, RoutedEventArgs e)
        {
            FlyOut_FindingMatches.Header = "Getting Tomorrow's matches, please wait...";
            FlyOut_FindingMatches.Visibility = Visibility.Visible;
            //Parser = new HTML_Parser("http://www.soccerstats.com/matches.asp?matchday=2");
            Parser.InitParser("http://www.soccerstats.com/matches.asp?matchday=2");

        }

        private void MetroWindow_Closing(object sender, CancelEventArgs e)
        {
            if (Parser != null)
            {
                Parser.CloseDriver();
            }
            System.Environment.Exit(1);

        }
    }
}
