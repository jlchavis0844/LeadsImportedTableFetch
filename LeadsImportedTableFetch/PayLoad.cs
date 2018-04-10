using System;

namespace LeadsImportedTableFetch {
    public class PayLoad {

        private string payload;
        private string cursor = "";
        private bool hits = false;
        private string dateString = "";

        public PayLoad() {
            dateString = DateTime.UtcNow.Date.ToString("yyyy-MM-ddTHH:mm:ssZ");
            LoadString();
        }

        private void LoadString() {
            payload = @"{'items': [{'data': {'query': {'projection':[ {'name':'custom_fields.332106' }, {'name': 'created_at' } ]," +
                "'filter': {'and': [{'filter': {'attribute': {'name': 'status.id' }, 'parameter': {'eq': 2127389 }}}, {'filter': " +
                "{'attribute': {'name': 'created_at' }, 'parameter': {'range': {'gte': '" + dateString + "' }}}}]}}, 'per_page': 100," +
                cursor + " 'hits': true }}]}";
        }

        public string GetCursor() {
            return cursor;
        }

        /// <summary>
        /// Adds the cursor to the payload. Used to mark page position for page 2+ of results
        /// </summary>
        /// <param name="curText">the cursor read from the POST call</param>
        public void SetCursor(string curText)
        {
            cursor = "'cursor': '" + curText + "',";
            LoadString();
        }

        public void ClearCursor()
        {
            cursor = "";
            LoadString();
        }

        public PayLoad(string inStr) {
            payload = inStr;
        }

        public override string ToString() {
            return payload.Replace(@"'", "\"");
        }
    }
}