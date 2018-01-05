using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

using Abp.Dependency;
using Abp.Runtime.Caching;

using DynamicTranslator.Application.Orchestrators.Detectors;
using DynamicTranslator.Application.Orchestrators.Finders;
using DynamicTranslator.Application.Orchestrators.Organizers;
using DynamicTranslator.Application.Requests;
using DynamicTranslator.Configuration.Startup;
using DynamicTranslator.Constants;
using DynamicTranslator.Domain.Events;
using DynamicTranslator.Domain.Model;
using DynamicTranslator.Service.GoogleAnalytics;
using DynamicTranslator.Wpf.Notification;
using RestSharp;
using DynamicTranslator.Wpf.ViewModel;
using Newtonsoft.Json;
using System.Net.Http;
using System.Windows.Media;

namespace DynamicTranslator.Wpf.Observers
{
    public class Finder : IObserver<EventPattern<WhenClipboardContainsTextEventArgs>>, ISingletonDependency
    {
        private const string Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
        private const string AcceptEncoding = "gzip, deflate, sdch";
        private const string AcceptLanguage = "en-US,en;q=0.8,tr;q=0.6";
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.80 Safari/537.36";
        private readonly ICacheManager _cacheManager;
        private readonly IDynamicTranslatorConfiguration _configuration;
        private readonly IGoogleAnalyticsService _googleAnalytics;
        private readonly ILanguageDetector _languageDetector;
        private readonly IMeanFinderFactory _meanFinderFactory;
        private readonly INotifier _notifier;
        private readonly IResultOrganizer _resultOrganizer;
        private string _previousString;
       
        public Finder(INotifier notifier,
            IMeanFinderFactory meanFinderFactory,
            IResultOrganizer resultOrganizer,
            ICacheManager cacheManager,
            IGoogleAnalyticsService googleAnalytics,
            ILanguageDetector languageDetector,
            IDynamicTranslatorConfiguration configuration)
        {
            _notifier = notifier;
            _meanFinderFactory = meanFinderFactory;
            _resultOrganizer = resultOrganizer;
            _cacheManager = cacheManager;
            _googleAnalytics = googleAnalytics;
            _languageDetector = languageDetector;
            _configuration = configuration;
           
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(EventPattern<WhenClipboardContainsTextEventArgs> value)
        {
            Task.Run(async () =>
            {
                try
                {
                    string currentString = value.EventArgs.CurrentString;

                    if (_previousString == currentString)
                    {
                        return;
                    }

                    _previousString = currentString;
                    RootObject resultDict = startCrawlerTraCau(_previousString).Result;
                    string resultVoca = "";
                    foreach(var result in resultDict.messages)
                    {
                        resultVoca += result.text + "\n";
                    }
                    string urlSound = resultDict.messages.LastOrDefault().attachment.payload.url.ToString();
                    MediaPlayer mplayer = new MediaPlayer() ;
                   var url = urlSound.Replace("https://", "http://");
                    mplayer.Open(new Uri(url));
                    mplayer.Play();
                    Maybe<string> findedMeans = new Maybe<string>(resultVoca);
                    
                    await Notify(currentString, findedMeans);
                   // await Trace(currentString, fromLanguageExtension);
                }
                catch (Exception ex)
                {
                  //  await Notify("Error", new Maybe<string>(ex.Message));
                }
            });
        }

        private async Task Trace(string currentString, string fromLanguageExtension)
        {
            await _googleAnalytics.TrackEventAsync("DynamicTranslator",
                "Translate",
                $"{currentString} | {fromLanguageExtension} - {_configuration.ApplicationConfiguration.ToLanguage.Extension} | v{ApplicationVersion.GetCurrentVersion()} ",
                null).ConfigureAwait(false);

            await _googleAnalytics.TrackAppScreenAsync("DynamicTranslator",
                ApplicationVersion.GetCurrentVersion(),
                "dynamictranslator",
                "dynamictranslator",
                "notification").ConfigureAwait(false);
        }

        private async Task Notify(string currentString, Maybe<string> findedMeans)
        {
            if (!string.IsNullOrEmpty(findedMeans.DefaultIfEmpty(string.Empty).First()))
            {
                await _notifier.AddNotificationAsync(currentString,
                    ImageUrls.NotificationUrl,
                    findedMeans.DefaultIfEmpty(string.Empty).First()
                );
            }
        }

        private Task<TranslateResult[]> GetMeansFromCache(string currentString, string fromLanguageExtension)
        {
            Task<TranslateResult[]> meanTasks = Task.WhenAll(_meanFinderFactory.GetFinders().Select(t => t.FindMean(new TranslateRequest(currentString, fromLanguageExtension))));

            return _cacheManager.GetCache<string, TranslateResult[]>(CacheNames.MeanCache)
                                .GetAsync(currentString, () => meanTasks);
        }
        public async Task<RootObject> startCrawlerTraCau(string voca)
        {
            
            var url = "http://olympusenglish.azurewebsites.net/Dictionary/callChatBot?contain="+voca;
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var response = await httpClient.PostAsync(url, null);
            string result = await response.Content.ReadAsStringAsync();
            RootObject dict = JsonConvert.DeserializeObject<RootObject>(result);
            return dict;
        }
    }
}
