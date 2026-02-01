using System;

namespace OsuTools
{
    namespace Api
    {
        /// <summary>
        /// osu.direct API search result data model.
        /// Corresponds to the JSON response from /api/v2/search
        /// </summary>
        [System.Serializable]
        public class OsuSearchResult
        {
            public int id;
            public string title;
            public string title_unicode;
            public string artist;
            public string artist_unicode;
            public string creator;
            public string source;
            public string tags;
            public Covers covers;
            public int favourite_count;
            public int? hype;
            public bool nsfw;
            public int offset;
            public int play_count;
            public string preview_url;
            public bool spotlight;
            public string status;
            public int? track_id;
            public int user_id;
            public bool video;
            public double bpm;
            public bool can_be_hyped;
            public string deleted_at;
            public bool discussion_enabled;
            public bool discussion_locked;
            public bool is_scoreable;
            public string last_updated;
            public string legacy_thread_url;
            public NominationsSummary nominations_summary;
            public int ranked;
            public string ranked_date;
            public bool storyboard;
            public string submitted_date;
            public Availability availability;
            public bool has_favourited;
            public BeatmapInfo[] beatmaps;
            public string[] pack_tags;
            public int[] modes;
            public string last_checked;
            public double rating;
            public int genre_id;
            public int language_id;
        }

        [System.Serializable]
        public class Covers
        {
            public string cover;
            public string cover_2x;  // JSON uses "cover@2x" but @ is not valid in C# identifiers
            public string card;
            public string card_2x;
            public string list;
            public string list_2x;
            public string slimcover;
            public string slimcover_2x;
        }

        [System.Serializable]
        public class NominationsSummary
        {
            public int current;
            public string[] eligible_main_rulesets;
            public RequiredMeta required_meta;
        }

        [System.Serializable]
        public class RequiredMeta
        {
            public int main_ruleset;
            public int non_main_ruleset;
        }

        [System.Serializable]
        public class Availability
        {
            public bool download_disabled;
            public string more_information;
        }

        [System.Serializable]
        public class BeatmapInfo
        {
            public int beatmapset_id;
            public double difficulty_rating;
            public int id;
            public string mode;
            public string status;
            public int total_length;
            public int user_id;
            public string version;
            public int accuracy;
            public double ar;
            public double bpm;
            public bool convert;
            public int count_circles;
            public int count_sliders;
            public int count_spinners;
            public double cs;
            public string deleted_at;
            public int drain;
            public int hit_length;
            public bool is_scoreable;
            public string last_updated;
            public int mode_int;
            public int passcount;
            public int playcount;
            public int ranked;
            public string url;
            public string checksum;
            public int max_combo;
        }

        /// <summary>
        /// Represents a search request with filtering options.
        /// </summary>
        [System.Serializable]
        public class SearchRequest
        {
            public string query;
            public int amount = 20;
            public int offset = 0;

            public SearchRequest(string query, int amount = 20, int offset = 0)
            {
                this.query = query;
                this.amount = amount;
                this.offset = offset;
            }
        }
    }
}
