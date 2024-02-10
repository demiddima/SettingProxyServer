using Renci.SshNet;
using System.Text.RegularExpressions;

namespace SettingProxyServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            /*TODO:
             * Генератор рандома для пароля, порта, логина
             * Отдельный обслуживающий класс для формирования строк и других данных (configText, получение gid and uid, получение nserver)
             */
            string host = "83.166.240.185";
            string username = "root";
            string password = "4mqMNmu7ne";
            Server serv = new Server(host, username, password);

            //Update
            string command = "sudo apt-get update";
            RunSshCommand(serv, command);

            //Install tar
            command = "apt install -y build-essential wget tar";
            RunSshCommand(serv, command);

            //Скачать репу
            command = "wget https://github.com/z3APA3A/3proxy/archive/0.9.4.tar.gz";
            RunSshCommand(serv, command);

            //Распаковка
            command = "tar -xvzf 0.9.4.tar.gz";
            RunSshCommand(serv, command);

            //Переход к папке и сборка
            List<string> commands = new List<string>
            {
                "cd 3proxy-0.9.4/",
                "make -f Makefile.Linux"
            };
            SendMultipleSshCommands(serv, commands);
            commands.Clear();

            //Создание директорий
            commands.Add("mkdir /etc/3proxy");
            commands.Add("mkdir -p /var/log/3proxy");
            commands.Add("cp bin/3proxy /usr/bin/");
            SendMultipleSshCommands(serv, commands);
            commands.Clear();

            //Пользователь для работы с конфигами
            command = "useradd -s /usr/sbin/nologin -U -M -r dies";
            RunSshCommand(serv, command);

            //Даем пользователю права на директории
            commands.Add("chown -R dies:dies /etc/3proxy");
            commands.Add("chown -R dies:dies /var/log/3proxy");
            commands.Add("chown -R dies:dies /usr/bin/3proxy");
            SendMultipleSshCommands(serv, commands);
            commands.Clear();

            //Даем пользователю права на директории
            commands.Add("touch /etc/3proxy/3proxy.cfg");
            commands.Add("chmod 600 /etc/3proxy/3proxy.cfg");
            SendMultipleSshCommands(serv, commands);
            commands.Clear();

            //Получение uid and gid
            command = "id dies";
            string result = RunSshCommand(serv, command);
            string uid = String.Empty;
            string gid = String.Empty;
            ParseIdOutput(result, out uid, out gid);

            //Получение nserver
            command = "cat /etc/resolv.conf";
            result = RunSshCommand(serv, command);
            string nserver = GetNameserverValue(result);

            //Создание файла конфига 3proxy.cfg
            string configText = @$"
                                users dies:CL:dHe6!kdS

                                setgid {gid}
                                setuid {uid}

                                nserver 8.8.8.8
                                nserver 8.8.4.4
                                nserver {nserver}

                                timeouts 1 5 30 60 180 1800 15 60
                                nscache 65536

                                proxy -p5128 -n -a -i0.0.0.0 -e

                                socks -p4380

                                log /var/log/3proxy/3proxy.log D
                                logformat ""- +_L%t.%. %N.%p %E %U %C:%c %R:%r %O %I %h %T""

                                rotate 30
                                ";
            string pathFile = "/etc/3proxy/3proxy.cfg";
            Set3proxyConf(serv, configText, pathFile);

            //Cоздание файла 3proxy.service
            commands.Add("touch /etc/systemd/system/3proxy.service");
            commands.Add("chmod 664 /etc/systemd/system/3proxy.service");
            SendMultipleSshCommands(serv, commands);
            commands.Clear();

            //Запись в файл 3proxy.service
            configText = @$"[Unit]
                            Description=3proxy Proxy Server
                            After=network.target
                            [Service]
                            Type=simple
                            ExecStart=/usr/bin/3proxy /etc/3proxy/3proxy.cfg
                            ExecStop=/bin/kill `/usr/bin/pgrep dies`
                            RemainAfterExit=yes
                            Restart=on-failure
                            [Install]
                            WantedBy=multi-user.target";
            pathFile = "/etc/systemd/system/3proxy.service";
            Set3proxyConf(serv, configText, pathFile);

            //Завершающие штрихи
            commands.Add("systemctl daemon-reload");
            commands.Add("systemctl start 3proxy");
            commands.Add("systemctl enable 3proxy");
            SendMultipleSshCommands(serv, commands);
            commands.Clear();

        }

        /// <summary>
        /// Одна команда
        /// </summary>
        /// <param name="server"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        public static string RunSshCommand(Server server, string command)
        {
            string result;
            try
            {
                using (var client = new SshClient(server.Host, server.Username, server.Password))
                {
                    client.Connect();

                    using (var sshCommand = client.CreateCommand(command))
                    {
                        sshCommand.CommandTimeout = TimeSpan.FromSeconds(120);
                        
                        // Запускаем выполнение команды
                        result = sshCommand.Execute();

                        // Выводим результат выполнения команды
                        Console.WriteLine("Command executed with result:");
                        Console.WriteLine(result);
                        
                    }

                    client.Disconnect();
                }
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                throw ex;
            }
        }

        /// <summary>
        /// Список команд
        /// </summary>
        /// <param name="server"></param>
        /// <param name="commands"></param>
        public static void SendMultipleSshCommands(Server server, List<string> commands)
        {
            try
            {
                using (var client = new SshClient(server.Host, server.Username, server.Password))
                {
                    client.Connect();

                    foreach (var command in commands)
                    {
                        Thread.Sleep(500);
                        Console.WriteLine($"Executing command: {command}");

                        using (var sshCommand = client.CreateCommand(command))
                        {
                            sshCommand.CommandTimeout = TimeSpan.FromSeconds(30);

                            // Запускаем выполнение команды
                            var result = sshCommand.Execute();

                            // Выводим результат выполнения команды
                            Console.WriteLine("Command executed with result:");
                            Console.WriteLine(result);
                        }
                    }

                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }

        /// <summary>
        /// Парсер uid
        /// </summary>
        /// <param name="output"></param>
        /// <param name="uid"></param>
        /// <param name="gid"></param>
        static void ParseIdOutput(string output,out string uid, out string gid)
        {
            // Разбор строки вывода
            string[] parts = output.Split(' ');

            uid = String.Empty;
            gid = String.Empty;

            foreach (string part in parts)
            {
                // Поиск строки, начинающейся с "uid=" или "gid="
                if (part.StartsWith("uid="))
                {
                    uid = part.Substring(4, part.IndexOf('(') - 4);
                    Console.WriteLine("UID: " + uid);
                }
                else if (part.StartsWith("gid="))
                {
                    gid = part.Substring(4, part.IndexOf('(') - 4);
                    Console.WriteLine("GID: " + gid);
                }
            }
        }

        /// <summary>
        /// парсер nameserver
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        static string GetNameserverValue(string input)
        {
            // Определение регулярного выражения для поиска значения nameserver
            Regex regex = new Regex(@"nameserver\s+(\S+)");

            // Поиск совпадений в тексте
            Match match = regex.Match(input);

            // Проверка наличия совпадений
            if (match.Success)
            {
                // Возвращаем найденное значение nameserver
                return match.Groups[1].Value;
            }
            else
            {
                return null; // Возвращаем null, если значение nameserver не найдено
            }
        }

        /// <summary>
        /// Запись в файл по пути
        /// </summary>
        /// <param name="server"></param>
        /// <param name="configText"></param>
        static void Set3proxyConf(Server server,string pathFile, string configText)
        {
            try
            {
                using (var client = new SshClient(server.Host, server.Username, server.Password))
                {
                    client.Connect();

                    //Запись в файл
                    var command = client.CreateCommand($"echo '{configText}' > {pathFile}");
                    command.Execute();


                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                
            }
        }
    }
}



