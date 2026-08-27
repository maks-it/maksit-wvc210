using MaksIT.Wvc210.Shared;

namespace MaksIT.Wvc210.Client;

public static class ConfigCatalog
{
    public static readonly IReadOnlyList<ChoiceOption> OffOn =
    [
        new("0", "Off"),
        new("1", "On")
    ];

    public static readonly IReadOnlyList<ChoiceOption> TimeZones = BuildTimeZones();

    public static IReadOnlyList<GroupDefinition> Groups { get; } =
    [
        new("SYSTEM", "Device", "Setup", "Camera name, clock, NTP, LED, and language.",
        [
            F("cfg_ver", "Config version", FieldKind.ReadOnly),
            F("host_name", "Device name", FieldKind.Text, "Up to 16 characters."),
            F("comment", "Description", FieldKind.Text, "Up to 32 characters."),
            Choice("time_format", "Time format", [new("24", "24-hour"), new("12", "12-hour")]),
            Choice("date_format", "Date format", [new("0", "Reserved 0"), new("1", "MM/DD/YYYY"), new("2", "DD/MM/YYYY")]),
            Choice("time_zone", "Time zone", TimeZones),
            Toggle("daylight_saving", "Daylight saving"),
            Toggle("ntp_mode", "NTP sync"),
            F("ntp_server", "NTP server", FieldKind.Text),
            Toggle("ntp_date", "NTP date override"),
            Int("ntp_hour", "NTP hour", 0, 23),
            Int("ntp_min", "NTP minute", 0, 59),
            Toggle("led_mode", "Status LED"),
            Choice("language_id", "Language",
            [
                new("0", "English"), new("2", "French"), new("3", "German"), new("4", "Swedish"),
                new("5", "Spanish"), new("6", "Italian"), new("7", "Portuguese"), new("8", "Danish"),
                new("9", "Dutch"), new("20", "American English"), new("21", "Simplified Chinese"),
                new("22", "Traditional Chinese"), new("23", "Australian English"),
                new("24", "Japanese"), new("25", "Korean")
            ])
        ]),
        new("NETWORK", "Network", "Setup", "IPv4, DHCP, and DNS.",
        [
            Toggle("dhcp", "DHCP"),
            F("ip_addr", "IP address", FieldKind.Text),
            F("netmask", "Subnet mask", FieldKind.Text),
            F("gateway", "Gateway", FieldKind.Text),
            Choice("dns_type", "DNS", [new("0", "From DHCP"), new("1", "Manual")]),
            F("dns_server1", "Primary DNS", FieldKind.Text),
            F("dns_server2", "Secondary DNS", FieldKind.Text)
        ]),
        new("WIRELESS", "Wireless", "Setup", "Wi-Fi, WEP/WPA, and WPS. Saving this group can drop the current link.",
        [
            Choice("wlan_type", "Mode", [new("0", "Ad hoc"), new("1", "Infrastructure")]),
            F("wlan_essid", "SSID", FieldKind.Text),
            Int("wlan_channel", "Channel (0 = auto)", 0, 13),
            Choice("wlan_domain", "Domain",
            [
                new("1", "Africa"), new("2", "Asia"), new("3", "Australia"), new("4", "Canada"),
                new("5", "Europe"), new("6", "Spain"), new("7", "France"), new("8", "Israel"),
                new("9", "Japan"), new("10", "Mexico"), new("11", "South America"), new("12", "USA")
            ]),
            Choice("wlan_security", "Security",
            [
                new("0", "None"), new("1", "WEP"), new("2", "WPA/WPA2-PSK"), new("3", "WPA PSK TKIP"),
                new("4", "WPA PSK AES"), new("5", "WPA2 PSK TKIP"), new("6", "WPA2 PSK AES"),
                new("7", "WPA Enterprise"), new("8", "WPA PSK"), new("9", "WPA2 PSK")
            ]),
            Choice("wep_authtype", "WEP auth", [new("1", "Open"), new("2", "Shared key")]),
            Choice("wep_mode", "WEP key mode",
            [
                new("1", "64-bit HEX"), new("2", "128-bit HEX"),
                new("3", "64-bit ASCII"), new("4", "128-bit ASCII")
            ]),
            Int("wep_index", "Active WEP key", 1, 4),
            F("wep_ascii", "WEP passphrase", FieldKind.Password),
            F("wep_kep1", "WEP key 1", FieldKind.Password),
            F("wep_kep2", "WEP key 2", FieldKind.Password),
            F("wep_kep3", "WEP key 3", FieldKind.Password),
            F("wep_kep4", "WEP key 4", FieldKind.Password),
            F("wpa_ascii", "WPA passphrase", FieldKind.Password),
            Toggle("wmm", "WMM"),
            Toggle("wps_mode", "WPS"),
            Choice("wpa_ep_auth_type", "Enterprise auth", [new("1", "EAP-TLS"), new("2", "EAP-TTLS")]),
            F("wpa_tls_user", "EAP-TLS user", FieldKind.Text),
            F("wpa_tls_priv_keypass", "EAP-TLS key password", FieldKind.Password),
            Choice("wpa_ttls_auth_type", "TTLS inner auth",
            [
                new("1", "MSCHAP"), new("2", "MSCHAPv2"), new("3", "PAP"),
                new("4", "EAP-MD5"), new("5", "EAP-GTC")
            ]),
            F("wpa_ttls_user", "TTLS user", FieldKind.Text),
            F("wpa_ttls_pass", "TTLS password", FieldKind.Password),
            F("wpa_ttls_anony_name", "Anonymous name", FieldKind.Text)
        ]),
        new("RTSP_RTP", "RTSP / RTP", "Advanced", "Streaming ports, packet size, and multicast.",
        [
            Int("rtsp_port", "RTSP port", 554, 65535),
            Int("rtp_port", "RTP port", 1024, 65535),
            Int("rtp_size", "RTP packet size", 400, 1400),
            Toggle("mcast_enable", "Multicast"),
            F("mcast_video_addr", "Video multicast address", FieldKind.Text),
            Int("mcast_video_port", "Video multicast port", 1024, 65534),
            F("mcast_audio_addr", "Audio multicast address", FieldKind.Text),
            Int("mcast_audio_port", "Audio multicast port", 1024, 65534),
            Int("mcast_hops", "Multicast TTL", 1, 255),
            F("mcast_group_name", "Multicast group name", FieldKind.Text)
        ]),
        new("UPNP", "UPnP", "Advanced", "Discovery and NAT traversal.",
        [
            Toggle("upnp_mode", "UPnP"),
            F("upnp_traversal", "Traversal", FieldKind.Text),
            F("upnp_camera", "Camera advertisement", FieldKind.Text)
        ]),
        new("QOS", "QoS", "Advanced", "DSCP marking for video traffic.",
        [
            Toggle("qos_enable", "QoS"),
            Int("qos_dscp", "DSCP (0–63)", 0, 63),
            Toggle("qos_av_switch", "AV switch")
        ]),
        new("LOG", "Logging", "Advanced", "Syslog, IM, and local log categories.",
        [
            Toggle("log_mode", "Logging"),
            Int("log_level", "Log level", 0, 7),
            Toggle("syslog_mode", "Syslog"),
            F("syslog_server", "Syslog server", FieldKind.Text),
            Int("syslog_port", "Syslog port", 1, 65535),
            Toggle("im_mode", "Instant message log"),
            F("im_server", "IM server", FieldKind.Text),
            F("im_account", "IM account", FieldKind.Text),
            F("im_password", "IM password", FieldKind.Password),
            F("im_sendto", "IM send to", FieldKind.Text),
            F("im_message", "IM message", FieldKind.Text),
            Toggle("ftplog_mode", "FTP log"),
            Toggle("smtplog_mode", "SMTP log"),
            Toggle("systemlog_mode", "System log"),
            Toggle("imlog_mode", "IM log")
        ]),
        new("VIDEO", "Image", "Audio / Video", "Overlay, frequency, color, and picture controls.",
        [
            Toggle("time_stamp", "Time stamp"),
            Toggle("text_overlay", "Text overlay"),
            F("text", "Overlay text", FieldKind.Text, "Up to 20 characters."),
            Choice("power_line", "Power line", [new("50", "50 Hz"), new("60", "60 Hz")]),
            Choice("color", "White balance",
            [
                new("0", "Auto"), new("1", "Indoor"), new("2", "White light"),
                new("3", "Yellow light"), new("4", "Outdoor"), new("5", "Black & white")
            ]),
            Int("exposure", "Exposure / brightness", 1, 7),
            Int("sharpness", "Sharpness", 1, 7),
            Int("hue", "Hue", 1, 7),
            Int("saturation", "Saturation", 1, 7),
            Int("contrast", "Contrast", 1, 7),
            Toggle("flip", "Flip"),
            Toggle("mirror", "Mirror"),
            Toggle("video_schedule", "Video schedule"),
            Choice("night_mode", "Night mode (unused on WVC210)",
            [
                new("0", "Off"),
                new("1", "On")
            ], "Present in firmware; this model has no IR-cut CGI. Use White balance → Black & white for a visible night picture."),
            Toggle("dn_filter", "Day/night IR-cut auto (unused on WVC210)",
                "PVC2300/WVC2300 leftover. WVC210 night picture is Color balance = Black & white."),
            F("dn_sch", "DN schedule", FieldKind.Text),
            Int("dn_sch_hr", "DN start hour", 0, 23),
            Int("dn_sch_min", "DN start minute", 0, 59),
            Int("dn_hrend", "DN end hour", 0, 23),
            Int("dn_minend", "DN end minute", 0, 59)
        ]),
        new("MPEG4", "MPEG-4", "Audio / Video", "Primary codec used by ASF and RTSP.",
        [
            Choice("resolution", "Resolution", [new("1", "160×120"), new("2", "320×240"), new("3", "640×480")]),
            Choice("quality_type", "Quality control", [new("0", "Fixed bit rate"), new("1", "Fixed quality")]),
            Choice("quality_level", "Quality",
            [
                new("1", "Very low"), new("2", "Low"), new("3", "Normal"),
                new("4", "High"), new("5", "Very high")
            ]),
            Choice("bit_rate", "Bit rate",
            [
                new("1", "64 kbps"), new("3", "128 kbps"), new("4", "256 kbps"),
                new("5", "384 kbps"), new("6", "512 kbps"), new("7", "768 kbps"),
                new("8", "1 Mbps"), new("9", "1.2 Mbps")
            ]),
            Int("frame_rate", "Frame rate", 1, 30),
            F("bandwidth", "Bandwidth", FieldKind.Text)
        ]),
        new("JPEG", "MJPEG", "Audio / Video", "Motion JPEG used by the in-app live view.",
        [
            Choice("resolution", "Resolution", [new("1", "160×120"), new("2", "320×240"), new("3", "640×480")]),
            Choice("quality_level", "Quality",
            [
                new("1", "Very low"), new("2", "Low"), new("3", "Normal"),
                new("4", "High"), new("5", "Very high")
            ]),
            Int("frame_rate", "Frame rate", 1, 30),
            F("bandwidth", "Bandwidth", FieldKind.Text)
        ]),
        new("MOBILE", "Mobile stream", "Audio / Video", "3GPP / mobile SDP stream.",
        [
            Toggle("mobile_support", "Enable"),
            Choice("resolution", "Resolution", [new("1", "160×120")]),
            Choice("quality_type", "Quality control", [new("0", "Fixed bit rate"), new("1", "Fixed quality")]),
            Choice("quality_level", "Quality",
            [
                new("1", "Very low"), new("2", "Low"), new("3", "Normal"),
                new("4", "High"), new("5", "Very high")
            ]),
            Choice("bit_rate", "Bit rate",
            [
                new("0", "32 kbps"), new("1", "64 kbps"), new("2", "96 kbps"),
                new("3", "128 kbps"), new("4", "256 kbps")
            ]),
            Int("frame_rate", "Frame rate", 1, 15),
            F("mobile_access", "Access path", FieldKind.Text)
        ]),
        new("AUDIO", "Audio", "Audio / Video", "Camera microphone, speaker, duplex mode, and the PC microphone used by Live speak.",
        [
            Toggle("audio_mode", "Audio enabled"),
            Toggle("audio_in", "Microphone in"),
            Int("in_volume", "Mic volume", 1, 16),
            Choice("in_audio_type", "Mic codec", [new("0", "G.711 A-law"), new("1", "G.711 µ-law")]),
            Toggle("audio_out", "Speaker out"),
            Int("out_volume", "Speaker volume", 1, 16),
            Choice("out_audio_type", "Speaker codec", [new("0", "G.711 A-law"), new("1", "G.711 µ-law")]),
            Choice("operation_mode", "Operation",
            [
                new("0", "Simplex listen"), new("1", "Simplex talk"),
                new("2", "Half duplex"), new("3", "Full duplex")
            ])
        ]),
        new("EMAIL", "Email", "Applications", "SMTP servers used by event alerts.",
        [
            Toggle("smtp_enable", "Primary SMTP"),
            Toggle("smtp_serv_flag", "Primary server flag"),
            F("smtp_server", "SMTP server", FieldKind.Text),
            F("pop_server", "POP server", FieldKind.Text),
            Int("smtp_port", "SMTP port", 1, 65535),
            Toggle("smtp_auth", "SMTP auth"),
            F("smtp_account", "SMTP user", FieldKind.Text),
            F("smtp_password", "SMTP password", FieldKind.Password),
            Toggle("smtp2_enable", "Secondary SMTP"),
            Toggle("smtp2_serv_flag", "Secondary server flag"),
            F("smtp2_server", "SMTP 2 server", FieldKind.Text),
            F("pop2_server", "POP 2 server", FieldKind.Text),
            Int("smtp2_port", "SMTP 2 port", 0, 65535),
            Toggle("smtp2_auth", "SMTP 2 auth"),
            F("smtp2_account", "SMTP 2 user", FieldKind.Text),
            F("smtp2_password", "SMTP 2 password", FieldKind.Password),
            F("from_addr", "From address", FieldKind.Text),
            F("from_addr2", "From address 2", FieldKind.Text),
            F("to_addr1", "To address 1", FieldKind.Text),
            F("to_addr2", "To address 2", FieldKind.Text),
            F("to_addr3", "To address 3", FieldKind.Text),
            F("send_email", "Send flags (bitmask)", FieldKind.Text, "A+B+C enable bits as used by the camera (e.g. 7 = all three)."),
            F("subject", "Subject", FieldKind.Text)
        ]),
        new("FTP", "FTP", "Applications", "Event upload servers.",
        [
            Toggle("ftp1", "FTP 1"),
            F("ftp1_server", "FTP 1 server", FieldKind.Text),
            F("ftp1_account", "FTP 1 user", FieldKind.Text),
            F("ftp1_passwd", "FTP 1 password", FieldKind.Password),
            F("ftp1_path", "FTP 1 path", FieldKind.Text),
            Toggle("ftp1_passive", "FTP 1 passive"),
            Int("ftp1_port", "FTP 1 port", 1, 65535),
            Toggle("ftp2", "FTP 2"),
            F("ftp2_server", "FTP 2 server", FieldKind.Text),
            F("ftp2_account", "FTP 2 user", FieldKind.Text),
            F("ftp2_passwd", "FTP 2 password", FieldKind.Password),
            F("ftp2_path", "FTP 2 path", FieldKind.Text),
            Toggle("ftp2_passive", "FTP 2 passive"),
            Int("ftp2_port", "FTP 2 port", 1, 65535)
        ]),
        new("SMBC", "SMB / CIFS", "Applications", "Windows share for events and continuous recording.",
        [
            Toggle("smbc_enable", "Event share"),
            F("smbc_server", "Event server", FieldKind.Text),
            F("smbc_path", "Event path", FieldKind.Text),
            F("smbc_account", "Event user", FieldKind.Text),
            F("smbc_passwd", "Event password", FieldKind.Password),
            Toggle("smbc_rec_enable", "Continuous recording"),
            F("smbc_rec_server", "Recording server", FieldKind.Text),
            F("smbc_rec_path", "Recording path", FieldKind.Text),
            Toggle("smbc_rec_file_ctrl", "File control"),
            Int("smbc_rec_filesize", "Max file size (KB)", 1, 1048576),
            Choice("smbc_rec_mode", "File mode",
            [
                new("0", "Single file, overwrite"),
                new("1", "Multiple files, timestamp names")
            ]),
            F("smbc_rec_account", "Recording user", FieldKind.Text),
            F("smbc_rec_passwd", "Recording password", FieldKind.Password)
        ]),
        new("MOTION", "Motion detection", "Applications", "Four windows on a 640×480 grid. Window 1 is full-screen when enabled.",
        [
            Toggle("md_mode", "Motion detection"),
            F("md_point", "PT motion point (X,Y)", FieldKind.Text, "X −63…63, Y −36…28."),
            Toggle("md_switch1", "Window 1 (full screen)"),
            F("md_name1", "Window 1 name", FieldKind.Text),
            F("md_window1", "Window 1 X1,Y1,X2,Y2", FieldKind.Text),
            Int("md_threshold1", "Window 1 threshold", 0, 255),
            Int("md_sensitivity1", "Window 1 sensitivity", 0, 10),
            Toggle("md_switch2", "Window 2"),
            F("md_name2", "Window 2 name", FieldKind.Text),
            F("md_window2", "Window 2 X1,Y1,X2,Y2", FieldKind.Text),
            Int("md_threshold2", "Window 2 threshold", 0, 255),
            Int("md_sensitivity2", "Window 2 sensitivity", 0, 10),
            Toggle("md_switch3", "Window 3"),
            F("md_name3", "Window 3 name", FieldKind.Text),
            F("md_window3", "Window 3 X1,Y1,X2,Y2", FieldKind.Text),
            Int("md_threshold3", "Window 3 threshold", 0, 255),
            Int("md_sensitivity3", "Window 3 sensitivity", 0, 10),
            Toggle("md_switch4", "Window 4"),
            F("md_name4", "Window 4 name", FieldKind.Text),
            F("md_window4", "Window 4 X1,Y1,X2,Y2", FieldKind.Text),
            Int("md_threshold4", "Window 4 threshold", 0, 255),
            Int("md_sensitivity4", "Window 4 sensitivity", 0, 10)
        ]),
        new("EVENT", "Events", "Applications", "What happens when motion (or I/O) fires.",
        [
            Toggle("event_trigger", "Event trigger"),
            Toggle("event_schedule", "Schedule"),
            Int("event_interval", "Interval (minutes)", 1, 15),
            F("event_mt", "Motion actions", FieldKind.Text, "Camera packed flags, e.g. email/FTP bits."),
            F("event_in1", "Input 1 actions", FieldKind.Text),
            F("event_in2", "Input 2 actions", FieldKind.Text),
            Choice("event_attach", "Attachment", [new("0", "MPEG-4"), new("1", "JPEG")]),
            F("event_mpeg4", "MPEG-4 capture", FieldKind.Text, "type,pre,post  — type 1=mp4 2=3gp 3=avi; pre 0–4s; post 1–5s; pre+post ≤ 5."),
            F("event_jpeg", "JPEG capture", FieldKind.Text, "count,pre,post  — count 1–4; pre 0–4s; post 1–5s."),
            F("event_define1", "Schedule 1", FieldKind.Text),
            F("event_define2", "Schedule 2", FieldKind.Text),
            F("event_define3", "Schedule 3", FieldKind.Text),
            F("event_define4", "Schedule 4", FieldKind.Text),
            F("event_define5", "Schedule 5", FieldKind.Text),
            F("event_define6", "Schedule 6", FieldKind.Text),
            F("event_define7", "Schedule 7", FieldKind.Text),
            F("event_define8", "Schedule 8", FieldKind.Text),
            F("event_define9", "Schedule 9", FieldKind.Text),
            F("event_define10", "Schedule 10", FieldKind.Text)
        ]),
        new("DDNS", "DDNS", "Applications", "Dynamic DNS client.",
        [
            Toggle("ddns_mode", "DDNS"),
            Choice("ddns_service", "Service",
            [
                new("1", "DynDNS.org"), new("2", "TZO.com"), new("3", "Reserved"),
                new("4", "FreeDNS"), new("5", "3322")
            ]),
            F("ddns_account", "Account", FieldKind.Text),
            F("ddns_password", "Password", FieldKind.Password),
            F("ddns_host_name", "Host name", FieldKind.Text),
            Int("ddns_hour", "Update hour", 0, 23),
            F("ddns_minute", "Update minute", FieldKind.Text),
            Choice("ddns_update_unit", "Update unit", [new("1", "Minute"), new("2", "Hour"), new("3", "Day")]),
            Int("ddns_update_period", "Update period", 1, 365)
        ]),
        new("PTZ", "Pan / Tilt", "Pan / Tilt", "Motor options, preset coordinates, patrol, and speeds.",
        [
            Toggle("PtzMode", "Pan/tilt enabled"),
            Choice("PtzMdMutex", "Motion vs PT",
            [
                new("0", "Disable PT while motion detection is on"),
                new("1", "Disable motion detection outside the MD area"),
                new("2", "Allow motion detection in all positions")
            ]),
            Toggle("GotoMdPosIdleEn", "Return to MD area after idle"),
            Int("GotoMdPosIdleVal", "Idle seconds", 60, 900),
            Int("PtzPanSpeed", "Pan speed", 1, 10),
            Int("PtzTiltSpeed", "Tilt speed", 1, 10),
            F("PredefineHome", "User home (X,Y)", FieldKind.Text),
            F("Patrol1Position", "Patrol sequence", FieldKind.Text, "preset,seconds;preset,seconds; … (5–60 s)"),
            F("PatrolInterval", "Patrol interval", FieldKind.Text),
            F("Preset1Name", "Preset 1 name", FieldKind.Text),
            F("Preset1Position", "Preset 1 X,Y", FieldKind.Text),
            F("Preset2Name", "Preset 2 name", FieldKind.Text),
            F("Preset2Position", "Preset 2 X,Y", FieldKind.Text),
            F("Preset3Name", "Preset 3 name", FieldKind.Text),
            F("Preset3Position", "Preset 3 X,Y", FieldKind.Text),
            F("Preset4Name", "Preset 4 name", FieldKind.Text),
            F("Preset4Position", "Preset 4 X,Y", FieldKind.Text),
            F("Preset5Name", "Preset 5 name", FieldKind.Text),
            F("Preset5Position", "Preset 5 X,Y", FieldKind.Text),
            F("Preset6Name", "Preset 6 name", FieldKind.Text),
            F("Preset6Position", "Preset 6 X,Y", FieldKind.Text),
            F("Preset7Name", "Preset 7 name", FieldKind.Text),
            F("Preset7Position", "Preset 7 X,Y", FieldKind.Text),
            F("Preset8Name", "Preset 8 name", FieldKind.Text),
            F("Preset8Position", "Preset 8 X,Y", FieldKind.Text),
            F("Preset9Name", "Preset 9 name", FieldKind.Text),
            F("Preset9Position", "Preset 9 X,Y", FieldKind.Text)
        ])
    ];

    private static FieldDefinition F(string key, string label, FieldKind kind, string? hint = null)
        => new(key, label, kind, hint);

    private static FieldDefinition Toggle(string key, string label, string? hint = null)
        => new(key, label, FieldKind.Toggle, hint, Choices: OffOn);

    private static FieldDefinition Choice(string key, string label, IReadOnlyList<ChoiceOption> choices, string? hint = null)
        => new(key, label, FieldKind.Choice, hint, Choices: choices);

    private static FieldDefinition Int(string key, string label, int min, int max, string? hint = null)
        => new(key, label, FieldKind.Integer, hint, min, max);

    private static IReadOnlyList<ChoiceOption> BuildTimeZones() =>
    [
        new("0", "(GMT-12:00) International Date Line West"),
        new("1", "(GMT-11:00) Midway"),
        new("2", "(GMT-10:00) Hawaii"),
        new("3", "(GMT-09:00) Alaska"),
        new("4", "(GMT-08:00) Pacific Time (US & Canada), Tijuana"),
        new("5", "(GMT-07:00) Arizona"),
        new("6", "(GMT-07:00) Chihuahua, La Paz, Mazatlan"),
        new("7", "(GMT-07:00) Mountain Time (US & Canada)"),
        new("8", "(GMT-06:00) Central America"),
        new("9", "(GMT-06:00) Central Time (US & Canada)"),
        new("10", "(GMT-06:00) Guadalajara, Mexico City, Monterrey"),
        new("11", "(GMT-06:00) Saskatchewan"),
        new("12", "(GMT-05:00) Bogota, Lima, Quito"),
        new("13", "(GMT-05:00) Eastern Time (US & Canada)"),
        new("14", "(GMT-05:00) Indiana (East)"),
        new("15", "(GMT-04:00) Atlantic Time (Canada)"),
        new("16", "(GMT-04:00) La Paz"),
        new("17", "(GMT-04:00) Santiago"),
        new("18", "(GMT-03:30) Newfoundland"),
        new("19", "(GMT-03:00) Brasilia"),
        new("20", "(GMT-03:00) Buenos Aires, Georgetown"),
        new("21", "(GMT-03:00) Greenland"),
        new("22", "(GMT-02:00) Mid-Atlantic"),
        new("23", "(GMT-01:00) Azores"),
        new("24", "(GMT-01:00) Cape Verde Is."),
        new("25", "(GMT) Casablanca, Monrovia"),
        new("26", "(GMT) Dublin, Edinburgh, Lisbon, London"),
        new("27", "(GMT+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna"),
        new("28", "(GMT+01:00) Belgrade, Bratislava, Budapest, Ljubljana, Prague"),
        new("29", "(GMT+01:00) Brussels, Copenhagen, Madrid, Paris"),
        new("30", "(GMT+01:00) Sarajevo, Skopje, Warsaw, Zagreb"),
        new("31", "(GMT+01:00) West Central Africa"),
        new("32", "(GMT+02:00) Athens, Istanbul, Minsk"),
        new("33", "(GMT+02:00) Bucharest"),
        new("34", "(GMT+02:00) Cairo"),
        new("35", "(GMT+02:00) Harare, Pretoria"),
        new("36", "(GMT+02:00) Helsinki, Kyiv, Riga, Sofia, Tallinn, Vilnius"),
        new("37", "(GMT+02:00) Jerusalem"),
        new("38", "(GMT+03:00) Baghdad"),
        new("39", "(GMT+03:00) Kuwait, Riyadh"),
        new("40", "(GMT+03:00) Moscow, St. Petersburg, Volgograd"),
        new("41", "(GMT+03:00) Nairobi"),
        new("42", "(GMT+03:30) Tehran"),
        new("43", "(GMT+04:00) Abu Dhabi, Muscat"),
        new("44", "(GMT+04:00) Baku, Tbilisi, Yerevan"),
        new("45", "(GMT+04:30) Kabul"),
        new("46", "(GMT+05:00) Ekaterinburg"),
        new("47", "(GMT+05:00) Islamabad, Karachi, Tashkent"),
        new("48", "(GMT+05:30) Chennai, Kolkata, Mumbai, New Delhi"),
        new("49", "(GMT+05:45) Kathmandu"),
        new("50", "(GMT+06:00) Almaty, Novosibirsk"),
        new("51", "(GMT+06:00) Astana, Dhaka"),
        new("52", "(GMT+06:00) Sri Jayawardenepura"),
        new("53", "(GMT+06:30) Rangoon"),
        new("54", "(GMT+07:00) Bangkok, Hanoi, Jakarta"),
        new("55", "(GMT+07:00) Krasnoyarsk"),
        new("56", "(GMT+08:00) Beijing, Chongqing, Hong Kong, Urumqi"),
        new("57", "(GMT+08:00) Irkutsk, Ulaan Bataar"),
        new("58", "(GMT+08:00) Kuala Lumpur, Singapore"),
        new("59", "(GMT+08:00) Perth"),
        new("60", "(GMT+08:00) Taipei"),
        new("61", "(GMT+09:00) Osaka, Sapporo, Tokyo"),
        new("62", "(GMT+09:00) Seoul"),
        new("63", "(GMT+09:00) Yakutsk"),
        new("64", "(GMT+09:30) Adelaide"),
        new("65", "(GMT+09:30) Darwin"),
        new("66", "(GMT+10:00) Brisbane"),
        new("67", "(GMT+10:00) Canberra, Melbourne, Sydney"),
        new("68", "(GMT+10:00) Guam, Port Moresby"),
        new("69", "(GMT+10:00) Hobart"),
        new("70", "(GMT+10:00) Vladivostok"),
        new("71", "(GMT+11:00) Magadan, Solomon Is., New Caledonia"),
        new("72", "(GMT+12:00) Auckland, Wellington"),
        new("73", "(GMT+12:00) Fiji, Kamchatka, Marshall Is."),
        new("74", "(GMT+13:00) Nuku'alofa"),
        new("75", "(GMT-04:30) Caracas")
    ];
}
