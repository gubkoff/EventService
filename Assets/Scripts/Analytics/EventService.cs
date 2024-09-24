using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Analytics
{
    public class EventService : MonoBehaviour
    {
        [SerializeField] private string _serverUrl;
        [SerializeField] private float _cooldownBeforeSend; 
        
        private float _currentTime;
        private bool _isCooldownTimerStarted;

        private Events _eventsData;
        private const string AnalyticsSaveKey = "ANALYTICS_SAVE_KEY";

        private bool _isSending;

        private void Start()
        {
            Initialize();
            StartCoroutine(Test());
        }

        private void Initialize()
        {
            _eventsData = new Events();
            if (!string.IsNullOrEmpty(GetAnalyticsJson()))
            {
                _eventsData = JsonConvert.DeserializeObject<Events>(GetAnalyticsJson());
                if (_eventsData.events.Count > 0)
                {
                    SendEvents().Forget();
                }
            }
        }

        private IEnumerator Test()
        {
            for (int i = 0; i < 100; i++)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(1, 10));
                TrackEvent($"event_{i}", $"data_{i}");
            }
        }

        public void TrackEvent(string type, string data)
        {
            if (!_isCooldownTimerStarted)
            {
                _isCooldownTimerStarted = true;
            }
            SaveEvents(type, data);
        }

        private void SaveEvents(string type, string data)
        {
            _eventsData.events.Add(new Event()
            {
                type = type,
                data = data
            });
            
            Save();
        }

        private void Save()
        {
            string json = JsonConvert.SerializeObject(_eventsData);
            PlayerPrefs.SetString(AnalyticsSaveKey, json);
            PlayerPrefs.Save();
        }

        private async UniTask SendEvents()
        {
            _isSending = true;

            string jsonData = GetAnalyticsJson();
            UnityWebRequest request = PostJson(new UnityWebRequest(_serverUrl), jsonData);

            try
            {
                await request.SendWebRequest();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Analytics] Analytics service event sending failed. Exception: {e}");
                
                request.Dispose();
                
                _isSending = false;
                return;
            }

            bool isSuccess = request.result == UnityWebRequest.Result.Success;
            
            if (!isSuccess)
            {
                Debug.LogError($"[Analytics] Analytics service event sending failed. Result: {request.result.ToString()} Error: {request.error}");
                request.Dispose();

                _isSending = false;
                return;
            }
            
            request.Dispose();
            
            _isSending = false;
            ClearSavedEvents();
        }
        
        private static UnityWebRequest PostJson(UnityWebRequest request, string json)
        {
            request.method = "POST";

            byte[] data = new System.Text.UTF8Encoding().GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(data);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");

            return request;
        }

        private void ClearSavedEvents()
        {
            _eventsData.events.Clear();
            Save();
        }

        private string GetAnalyticsJson()
        {
            if (PlayerPrefs.HasKey(AnalyticsSaveKey))
            {
                string json = PlayerPrefs.GetString(AnalyticsSaveKey);

                if (json != null)
                {
                    return json;
                }
            }

            return string.Empty;
        }
        
        private void Update()
        {
            Cooldown();
        }

        private void Cooldown()
        {
            if(!_isCooldownTimerStarted || _isSending) return;
            
            if (_currentTime < _cooldownBeforeSend)
            {
                _currentTime += Time.deltaTime;
                return;
            }
            
            _isCooldownTimerStarted = false;
            _currentTime = 0;
            SendEvents().Forget();
        }
    }
}

