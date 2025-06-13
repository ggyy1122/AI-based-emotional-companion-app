using Newtonsoft.Json;
using System.Collections.Generic;

namespace GameApp.Services.Voice.Models
{
    public class BaiduTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("error_description")]
        public string ErrorDescription { get; set; }
    }

    public class BaiduSpeechRequest
    {
        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("rate")]
        public int Rate { get; set; }

        [JsonProperty("channel")]
        public int Channel { get; set; }

        [JsonProperty("cuid")]
        public string Cuid { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("speech")]
        public string Speech { get; set; }

        [JsonProperty("len")]
        public int Len { get; set; }

        [JsonProperty("dev_pid")]
        public int DevPid { get; set; } = 1537;
    }

    public class BaiduSpeechResponse
    {
        [JsonProperty("err_msg")]
        public string ErrMsg { get; set; }

        [JsonProperty("err_no")]
        public int ErrNo { get; set; }

        [JsonProperty("result")]
        public List<string> Result { get; set; }

        public bool IsSuccess => ErrNo == 0;

        public string RecognizedText => Result != null && Result.Count > 0 ? Result[0] : "";
    }
}
