using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KancolleBgmChenger
{
    class TimePeriodJudge
    {
        private TimePeriodJudge()
        { }

        ~TimePeriodJudge()
        { }

        static public TimePeriod GetTimePeriod()
        {
            System.DateTime dt_today_zero_time = System.DateTime.Today; //日付が今日、時刻分秒が0時0分0秒のオブジェクト
            System.DateTime dt_now = System.DateTime.Now;
            /*
            System.DateTime dt_now = System.DateTime.Today;
            dt_now = dt_now.AddHours(18);
            dt_now = dt_now.AddMinutes(0);
            dt_now = dt_now.AddSeconds(0);
            */
            TimePeriod time_period = TimePeriod.TIME_PERIOD_DAYTIME;

            if (dt_today_zero_time <= dt_now && dt_now < dt_today_zero_time.AddHours(timeMorinigStart))
            {
                // 夜
                time_period = TimePeriod.TIME_PERIOD_NIGHT;
            }
            else if (dt_today_zero_time.AddHours(timeMorinigStart) <= dt_now && dt_now < dt_today_zero_time.AddHours(timeDayTimeStart))
            {
                // 朝
                time_period = TimePeriod.TIME_PERIOD_MORNING;
            }
            else if (dt_today_zero_time.AddHours(timeDayTimeStart) <= dt_now && dt_now < dt_today_zero_time.AddHours(timeEveningStart))
            {
                // 昼
                time_period = TimePeriod.TIME_PERIOD_DAYTIME;
            }
            else if (dt_today_zero_time.AddHours(timeEveningStart) <= dt_now && dt_now < dt_today_zero_time.AddHours(timeNightStart))
            {
                // 夕方
                time_period = TimePeriod.TIME_PERIOD_EVENING;
            }
            else if (dt_today_zero_time.AddHours(timeNightStart) <= dt_now)
            {
                // 夜
                // 23:59:59に母港にやってきた場合も対応可
                // todayの取得が先、nowの取得が後なので、1/1 0:0:0 (today) <= 1/2 0:0:0(now)となってこの分岐に入る
                time_period = TimePeriod.TIME_PERIOD_NIGHT;
            }
            else{
                // ここには来ないはず
            }

            return time_period;
        }

        private const uint timeMorinigStart = 4;
        private const uint timeDayTimeStart = 6;
        private const uint timeEveningStart = 16;
        private const uint timeNightStart = 18;
        private const uint timeDayEnd = 24;

        public enum TimePeriod : uint
        {
            TIME_PERIOD_MORNING　= 0,
            TIME_PERIOD_DAYTIME,
            TIME_PERIOD_EVENING,
            TIME_PERIOD_NIGHT
        }
    }
}
