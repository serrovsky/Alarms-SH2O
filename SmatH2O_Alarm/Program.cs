using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Configuration;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Globalization;

namespace SmatH2O_Alarm
{
    class Program
    {
        static private string strValidateMsg;
        static private bool isValid = true;
        static XmlDocument alarmRules;

        static String ipAddress = ConfigurationSettings.AppSettings["ipAddressMessagingChannel"];
        static MqttClient m_cClient = new MqttClient("127.0.0.1");
        static string[] m_strTopicsInfo = { "dataSensors", "alarms" };

        static int Main(string[] args)
        {
            alarmRules = new XmlDocument();
            alarmRules.Load(@"triggerRules.xml");

            if (!validateXml(alarmRules))
            {
                Console.WriteLine("ALARM RULES XML WITH BAD CONFIGURATION");
                return 1;
            }

            connectToMessagingChannel();

            return 0;
        }

        private static void connectToMessagingChannel()
        {
            m_cClient.Connect(Guid.NewGuid().ToString());

            if (!m_cClient.IsConnected)
            {
                Console.WriteLine("Error connecting to message broker...");
                return;
            }

            m_cClient.MqttMsgPublishReceived += client_MqttMsgPublishReceived;

            byte[] qosLevels = { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE };//QoS

            m_cClient.Subscribe(m_strTopicsInfo, qosLevels);
        }


        static void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            String strTemp = Encoding.UTF8.GetString(e.Message);

            if (e.Topic == "dataSensors")
            {
                analiseDataSensor(strTemp);
            }
        }

        private static void analiseDataSensor(string strTemp)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(strTemp);

            XmlNode sensorType = doc.SelectSingleNode("signal/@parameterType");
            XmlNode sensorId = doc.SelectSingleNode("signal/@parameterId");
            XmlNode currentValue = doc.SelectSingleNode("signal/value");

            XmlNode alarm = alarmRules.SelectSingleNode("alarmRules/" + sensorType.InnerText);

            if (alarm.Attributes["alarmStatus"].Value == "ON")
            {
                XmlNodeList sensorRules = alarmRules.SelectNodes("alarmRules/" + sensorType.InnerText + "/rule[@ruleStatus='" + "ON" + "']");

                foreach (XmlNode x in sensorRules)
                {

                    string condition = x.Attributes["condition"].Value;
                    XmlNode alarmValue = x.SelectSingleNode("value");

                    switch (condition)
                    {
                        case "GREATERTHAN":
                            checkValue("GREATERTHAN", currentValue.InnerText, alarmValue.InnerText, sensorType.InnerText, sensorId.InnerText);
                            break;
                        case "LESSTHAN":
                            checkValue("LESSTHAN", currentValue.InnerText, alarmValue.InnerText, sensorType.InnerText, sensorId.InnerText);
                            break;
                        case "EQUALS":
                            checkValue("EQUALS", currentValue.InnerText, alarmValue.InnerText, sensorType.InnerText, sensorId.InnerText);
                            break;
                        case "BETWEEN":
                            XmlNode alarmMinValue = x.SelectSingleNode("minValue");
                            XmlNode alarmMaxValue = x.SelectSingleNode("maxValue");
                            checkValueBetween(currentValue.InnerText, alarmMinValue.InnerText, alarmMaxValue.InnerText, sensorType.InnerText, sensorId.InnerText);
                            break;
                    }
                }
            }
        }

        private static void checkValueBetween(string currentValue, string minValue, string maxValue, string sensorType, string sensorId)
        {
            float currentVal = float.Parse(currentValue, CultureInfo.InvariantCulture.NumberFormat);
            float alarmMinVal = float.Parse(minValue, CultureInfo.InvariantCulture.NumberFormat);
            float alarmMaxVal = float.Parse(maxValue, CultureInfo.InvariantCulture.NumberFormat);

            if (alarmMinVal > currentVal && currentVal > alarmMaxVal)
            {
                alarmTriggerBetween(currentValue, minValue, maxValue, sensorType, sensorId);
            }
        }


        private static void checkValue(string operation, string currentValue, string alarmValue, string sensorType, string sensorId)
        {
            float currentVal = float.Parse(currentValue, CultureInfo.InvariantCulture.NumberFormat);
            float alarmVal = float.Parse(alarmValue, CultureInfo.InvariantCulture.NumberFormat);

            switch (operation)
            {
                case "EQUALS":
                    if (currentVal == alarmVal)
                    {
                        alarmtrigger(operation, currentValue, alarmValue, sensorType, sensorId);
                    }
                    break;
                case "GREATERTHAN":
                    if (currentVal > alarmVal)
                    {
                        alarmtrigger(operation, currentValue, alarmValue, sensorType, sensorId);
                    }
                    break;
                case "LESSTHAN":
                    if (currentVal < alarmVal)
                    {
                        alarmtrigger(operation, currentValue, alarmValue, sensorType, sensorId);
                    }
                    break;
            }
        }


        private static void alarmtrigger(string operation, string currentValue, string alarmValue, string sensorType, string sensorId)
        {

            XmlDocument alarmString = new XmlDocument();

            DateTime dat = DateTime.Now;
            DateTimeFormatInfo dfi = DateTimeFormatInfo.CurrentInfo;
            Calendar cal = dfi.Calendar;
            int weekNumber = cal.GetWeekOfYear(dat, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Sunday);

            String d = dat.ToString("HH;mm;ss;dd;MM;yyyy");

            String[] parsedDate = d.Split(';');

            XmlElement alarm = alarmString.CreateElement("alarm");
            alarm.SetAttribute("parameterType", sensorType);
            alarm.SetAttribute("parameterId", sensorId);

            XmlElement value = alarmString.CreateElement("value");
            value.InnerText = currentValue;

            XmlElement alarmDescription = alarmString.CreateElement("alarmDescription");
            alarmDescription.InnerText = operation;

            XmlElement date = alarmString.CreateElement("date");
            XmlElement day = alarmString.CreateElement("day");
            day.InnerText = parsedDate[3];
            XmlElement month = alarmString.CreateElement("month");
            month.InnerText = parsedDate[4];
            XmlElement year = alarmString.CreateElement("year");
            year.InnerText = parsedDate[5];
            XmlElement hour = alarmString.CreateElement("hour");
            hour.InnerText = parsedDate[0];
            XmlElement minute = alarmString.CreateElement("minute");
            minute.InnerText = parsedDate[1];
            XmlElement second = alarmString.CreateElement("second");
            second.InnerText = parsedDate[2];
            XmlElement week = alarmString.CreateElement("week");
            week.InnerText = weekNumber.ToString();

            alarm.AppendChild(value);

            if (operation == "GREATERTHAN")
            {
                XmlElement maxLimit = alarmString.CreateElement("maxLimit");
                maxLimit.InnerText = alarmValue;
                alarm.AppendChild(maxLimit);
            }
            else if (operation == "LESSTHAN")
            {
                XmlElement minLimit = alarmString.CreateElement("minLimit");
                minLimit.InnerText = alarmValue;
                alarm.AppendChild(minLimit);
            }


            alarm.AppendChild(alarmDescription);

            date.AppendChild(day);
            date.AppendChild(month);
            date.AppendChild(year);
            date.AppendChild(hour);
            date.AppendChild(minute);
            date.AppendChild(second);
            date.AppendChild(week);
            alarm.AppendChild(date);

            alarm.AppendChild(date);

            alarmString.AppendChild(alarm);

            sendAlarmToMessaging(alarmString.OuterXml);

        }

        private static void alarmTriggerBetween(string currentValue, string minValue, string maxValue, string sensorType, string sensorId)
        {
            XmlDocument alarmString = new XmlDocument();

            DateTime dat = DateTime.Now;
            DateTimeFormatInfo dfi = DateTimeFormatInfo.CurrentInfo;
            Calendar cal = dfi.Calendar;
            int weekNumber = cal.GetWeekOfYear(dat, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Sunday);

            String d = dat.ToString("HH;mm;ss;dd;MM;yyyy");

            String[] parsedDate = d.Split(';');

            XmlElement alarm = alarmString.CreateElement("alarm");
            alarm.SetAttribute("parameterType", sensorType);
            alarm.SetAttribute("parameterId", sensorId);

            XmlElement value = alarmString.CreateElement("value");
            value.InnerText = currentValue;

            XmlElement maxLimit = alarmString.CreateElement("maxLimit");
            maxLimit.InnerText = maxValue;
            XmlElement minLimit = alarmString.CreateElement("minLimit");
            minLimit.InnerText = minValue;

            XmlElement alarmDescription = alarmString.CreateElement("alarmDescription");
            alarmDescription.InnerText = "BETWEEN";

            XmlElement date = alarmString.CreateElement("date");
            XmlElement day = alarmString.CreateElement("day");
            day.InnerText = parsedDate[3];
            XmlElement month = alarmString.CreateElement("month");
            month.InnerText = parsedDate[4];
            XmlElement year = alarmString.CreateElement("year");
            year.InnerText = parsedDate[5];
            XmlElement hour = alarmString.CreateElement("hour");
            hour.InnerText = parsedDate[0];

            XmlElement minute = alarmString.CreateElement("minute");
            minute.InnerText = parsedDate[1];
            XmlElement second = alarmString.CreateElement("second");
            second.InnerText = parsedDate[2];
            XmlElement week = alarmString.CreateElement("week");
            week.InnerText = weekNumber.ToString();


            date.AppendChild(day);
            date.AppendChild(month);
            date.AppendChild(year);
            date.AppendChild(hour);
            date.AppendChild(minute);
            date.AppendChild(second);
            date.AppendChild(week);
            alarm.AppendChild(date);


            alarm.AppendChild(alarmDescription);
            alarm.AppendChild(date);

            alarmString.AppendChild(alarm);

            sendAlarmToMessaging(alarmString.OuterXml);

        }

        /*private static XmlElement createDateXML()
        {
            DateTime dat = DateTime.Now;
            DateTimeFormatInfo dfi = DateTimeFormatInfo.CurrentInfo;
            Calendar cal = dfi.Calendar;
            int weekNumber = cal.GetWeekOfYear(dat, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Sunday);

            String d = dat.ToString("HH;mm;ss;dd;MM;yyyy");

            String[] parsedDate = d.Split(';');
            XmlElement date = new XmlElement("date");
            XmlElement date = alarmString.CreateElement("date");
            XmlElement day = alarmString.CreateElement("day");
            day.InnerText = parsedDate[3];
            XmlElement month = alarmString.CreateElement("month");
            month.InnerText = parsedDate[4];
            XmlElement year = alarmString.CreateElement("year");
            year.InnerText = parsedDate[5];
            XmlElement hour = alarmString.CreateElement("hour");
            hour.InnerText = parsedDate[0];
            XmlElement minute = alarmString.CreateElement("minute");
            minute.InnerText = parsedDate[1];
            XmlElement second = alarmString.CreateElement("second");
            second.InnerText = parsedDate[2];
            XmlElement week = alarmString.CreateElement("week");
            week.InnerText = weekNumber.ToString();

            date.AppendChild(day);
            date.AppendChild(month);
            date.AppendChild(year);
            date.AppendChild(hour);
            date.AppendChild(minute);
            date.AppendChild(second);
            date.AppendChild(week);

            return date;

        }*/

        private static void sendAlarmToMessaging(string alarmMessage)
        {
            if (m_cClient.IsConnected)
            {
                m_cClient.Publish(m_strTopicsInfo[1], Encoding.UTF8.GetBytes(alarmMessage));
            }
        }


        private static bool validateXml(XmlDocument alarmRules)
        {
            alarmRules.Schemas.Add(null, @"triggerRules.xsd");
            ValidationEventHandler handler = new ValidationEventHandler(MyValidationMethod);

            alarmRules.Validate(handler);

            if (isValid)
            {
                Console.WriteLine("OK");
            }
            else
            {
                Console.WriteLine("INVALID" + strValidateMsg);
            }
            return isValid;
        }

        private static void MyValidationMethod(object sender, ValidationEventArgs args)
        {
            isValid = false;

            switch (args.Severity)
            {
                case XmlSeverityType.Error:
                    strValidateMsg = "Error" + args.Message;
                    break;
                case XmlSeverityType.Warning:
                    strValidateMsg = "Warning" + args.Message;
                    break;
                default:
                    break;
            }
        }
    }
}
