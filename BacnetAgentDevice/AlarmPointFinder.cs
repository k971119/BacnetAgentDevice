using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BacnetAgentDevice
{
    public class AlarmPointFinder
    {
        String ConnectionString = "Server=192.168.10.51;database=IBSInfo;uid=sa;pwd=admin123!@#";

        String alarmSelectQuery = "SELECT SYSTEM_PT_ID FROM P_OBJECT WHERE ALARM_LV = 1 AND DEVICE_SEQ = {0}";

        String pt_ids = String.Empty;
        int pointsCount = 0;
        public AlarmPointFinder(int deviceSeq)
        {

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                SqlCommand command = new SqlCommand(String.Format(alarmSelectQuery, deviceSeq), conn);
                SqlDataReader reader = command.ExecuteReader();
                bool first = true;
                while (reader.Read())
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        pt_ids += ",";
                    }
                    pt_ids += reader.GetString(0);
                    pointsCount++;
                }
            }
        }

        public String getAlarmPoints()
        {
            return pt_ids;
        }
        public int getAlarmPointsCount()
        {
            return pointsCount;
        }
    }
}
