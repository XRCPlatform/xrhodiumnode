## BRhodium DNS Crawler 
The BRhodium DNS Crawler provides a list of BRhodium full nodes that have recently been active via a custom DNS server.

### Prerequisites

To install and run the DNS Server, you need
* [.NET Core 2.0](https://www.microsoft.com/net/download/core)
* [Git](https://git-scm.com/)

## Build instructions

### Get the repository and its dependencies

```
git clone https://github.com/BRhodiumproject/BRhodiumBitcoinFullNode.git  
cd BRhodiumBitcoinFullNode
git submodule update --init --recursive
```

### Build and run the code
With this node, you can run the DNS Server in isolation or as a BRhodium node with DNS functionality:

1. To run a <b>BRhodium</b> node <b>only</b> on <b>MainNet</b>, do
```
cd BRhodium.BRhodiumDnsD
dotnet run -dnslistenport=5399 -dnshostname=dns.BRhodiumplatform.com -dnsnameserver=ns1.dns.BRhodiumplatform.com -dnsmailbox=admin@BRhodiumplatform.com
```  

2. To run a <b>BRhodium</b> node and <b>full node</b> on <b>MainNet</b>, do
```
cd BRhodium.BRhodiumDnsD
dotnet run -dnsfullnode -dnslistenport=5399 -dnshostname=dns.BRhodiumplatform.com -dnsnameserver=ns1.dns.BRhodiumplatform.com -dnsmailbox=admin@BRhodiumplatform.com
```  

3. To run a <b>BRhodium</b> node <b>only</b> on <b>TestNet</b>, do
```
cd BRhodium.BRhodiumDnsD
dotnet run -testnet -dnslistenport=5399 -dnshostname=dns.BRhodiumplatform.com -dnsnameserver=ns1.dns.BRhodiumplatform.com -dnsmailbox=admin@BRhodiumplatform.com
```  

4. To run a <b>BRhodium</b> node and <b>full node</b> on <b>TestNet</b>, do
```
cd BRhodium.BRhodiumDnsD
dotnet run -testnet -dnsfullnode -dnslistenport=5399 -dnshostname=dns.BRhodiumplatform.com -dnsnameserver=ns1.dns.BRhodiumplatform.com -dnsmailbox=admin@BRhodiumplatform.com
```  

### Command-line arguments

| Argument      | Description                                                                          |
| ------------- | ------------------------------------------------------------------------------------ |
| dnslistenport | The port the BRhodium DNS Server will listen on                                       |
| dnshostname   | The host name for BRhodium DNS Server                                                 |
| dnsnameserver | The nameserver host name used as the authoritative domain for the BRhodium DNS Server |
| dnsmailbox    | The e-mail address used as the administrative point of contact for the domain        |

### NS Record

Given the following settings for the BRhodium DNS Server:

| Argument      | Value                             |
| ------------- | --------------------------------- |
| dnslistenport | 53                                |
| dnshostname   | BRhodiumdns.BRhodiumplatform.com    |
| dnsnameserver | ns.BRhodiumdns.BRhodiumplatform.com |

You should have NS and A record in your ISP DNS records for your DNS host domain:

| Type     | Hostname                          | Data                              |
| -------- | --------------------------------- | --------------------------------- |
| NS       | BRhodiumdns.BRhodiumplatform.com    | ns.BRhodiumdns.BRhodiumplatform.com |
| A        | ns.BRhodiumdns.BRhodiumplatform.com | 192.168.1.2                       |

To verify the BRhodium DNS Server is running with these settings run:

```
dig +qr -p 53 BRhodiumdns.BRhodiumplatform.com
```  
or
```
nslookup BRhodiumdns.BRhodiumplatform.com
```
