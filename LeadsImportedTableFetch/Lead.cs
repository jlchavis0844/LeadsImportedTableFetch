using System;

namespace LeadsImportedTableFetch {
    public class Lead {
        public int BaseID { get; set; }
        public int ClientID { get; set; }
        public DateTime created_at { get; set; }

    public Lead(int BaseID, int ClientID, DateTime created_at) {
            this.BaseID = BaseID;
            this.ClientID = ClientID;
            this.created_at = created_at;
        }
    }
}