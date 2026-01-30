import{_ as b,u as L,g as T,b as e,c as a,d as t,F as v,r as x,t as c,e as f,w as _,i as S,n as B,j as M,f as g,k as d}from"./index-vrOceqiF.js";const D={class:"vp-sidebar"},E={class:"sidebar-content"},y={class:"group-title"},I={class:"group-items"},O={__name:"VPSidebar",props:{items:{type:Array,required:!0}},setup(k){const r=L(),p=l=>r.path===l||r.path.startsWith(l+"/");return(l,u)=>{const s=T("router-link");return e(),a("aside",D,[t("div",E,[(e(!0),a(v,null,x(k.items,i=>(e(),a("div",{key:i.text,class:"sidebar-group"},[t("p",y,c(i.text),1),t("ul",I,[(e(!0),a(v,null,x(i.items,o=>(e(),a("li",{key:o.link},[f(s,{to:o.link,class:B(["sidebar-link",{active:p(o.link)}])},{default:_(()=>[S(c(o.text),1)]),_:2},1032,["to","class"])]))),128))])]))),128))])])}}},A=b(O,[["__scopeId","data-v-9c29086b"]]),V={class:"guide-page"},F={class:"guide-content"},q=["innerHTML"],w={key:0,class:"page-nav"},C={class:"nav-title"},N={class:"nav-title"},U={__name:"Guide",setup(k){const r=L(),p=[{text:"入门指南",items:[{text:"快速开始",link:"/guide/getting-started"},{text:"安装说明",link:"/guide/installation"},{text:"常见问题",link:"/guide/faq"}]},{text:"平台教程",items:[{text:"高通 EDL 模式",link:"/guide/qualcomm"},{text:"MTK 联发科",link:"/guide/mtk"},{text:"展锐 Spreadtrum",link:"/guide/spd"},{text:"Fastboot 模式",link:"/guide/fastboot"}]},{text:"进阶功能",items:[{text:"云端 Loader",link:"/guide/cloud-loader"},{text:"自动认证",link:"/guide/auto-auth"},{text:"分区操作",link:"/guide/partitions"}]}],l=p.flatMap(n=>n.items),u=d(()=>l.findIndex(n=>n.link===r.path)),s=d(()=>u.value>0?l[u.value-1]:null),i=d(()=>u.value<l.length-1?l[u.value+1]:null),o={"getting-started":`
    <h1>快速开始</h1>
    <p>欢迎使用 SakuraEDL！本指南将帮助你快速上手。</p>
    
    <h2>安装软件</h2>
    <ol>
      <li>下载最新版 SakuraEDL</li>
      <li>解压到任意目录</li>
      <li>以管理员身份运行 <code>SakuraEDL.exe</code></li>
    </ol>
    
    <h2>安装驱动</h2>
    <p>根据你的设备平台，安装对应的驱动程序：</p>
    <ul>
      <li><strong>高通设备</strong>：安装 Qualcomm 9008 驱动</li>
      <li><strong>MTK 设备</strong>：安装 MediaTek USB 驱动</li>
      <li><strong>展锐设备</strong>：安装 Spreadtrum 驱动</li>
    </ul>
    
    <h2>连接设备</h2>
    <ol>
      <li>将设备进入对应的刷机模式（EDL/BROM/研发模式）</li>
      <li>使用 USB 数据线连接电脑</li>
      <li>软件会自动识别设备并显示端口</li>
    </ol>
    
    <h2>下一步</h2>
    <p>根据你的设备平台，查看对应的详细教程。</p>
  `,installation:`
    <h1>安装说明</h1>
    
    <h2>系统要求</h2>
    <ul>
      <li>Windows 7/8/10/11 (64位)</li>
      <li>.NET Framework 4.8</li>
      <li>管理员权限</li>
    </ul>
    
    <h2>安装步骤</h2>
    <ol>
      <li>从官网或 GitHub 下载最新版本</li>
      <li>解压 ZIP 压缩包到任意目录（建议英文路径）</li>
      <li>右键点击 <code>SakuraEDL.exe</code>，选择"以管理员身份运行"</li>
    </ol>
    
    <h2>驱动安装</h2>
    <p>首次使用需要安装设备驱动，软件会自动提示安装。</p>
  `,faq:`
    <h1>常见问题</h1>
    
    <h2>设备无法识别</h2>
    <p>请检查以下几点：</p>
    <ul>
      <li>驱动是否正确安装</li>
      <li>USB 数据线是否支持数据传输</li>
      <li>设备是否正确进入刷机模式</li>
    </ul>
    
    <h2>Loader 签名验证失败</h2>
    <p>这通常意味着 Loader 与设备不匹配。请尝试使用云端自动匹配功能。</p>
    
    <h2>刷机失败</h2>
    <p>请检查镜像文件是否完整，以及是否选择了正确的分区。</p>
  `,qualcomm:`
    <h1>高通 EDL 模式</h1>
    <p>高通 EDL (Emergency Download) 模式是高通芯片的底层刷机模式。</p>
    
    <h2>进入 EDL 模式</h2>
    <p>不同设备进入方式略有不同：</p>
    <ul>
      <li><strong>小米</strong>：关机后按住音量-和电源键</li>
      <li><strong>一加</strong>：使用工程线或 ADB 命令</li>
      <li><strong>OPPO</strong>：使用深度测试或 ADB 命令</li>
    </ul>
    
    <h2>自动认证</h2>
    <p>新款设备需要认证才能刷机：</p>
    <ul>
      <li><strong>小米设备</strong>：自动执行小米 Auth 认证</li>
      <li><strong>一加设备</strong>：勾选 OnePlus 验证选项</li>
      <li><strong>VIP 设备</strong>：需要 VIP 资源包</li>
    </ul>
    
    <h2>操作流程</h2>
    <ol>
      <li>选择正确的 COM 端口</li>
      <li>选择 Loader 文件（或使用云端自动匹配）</li>
      <li>点击"连接"建立通信</li>
      <li>选择要操作的分区进行读写</li>
    </ol>
  `,mtk:`
    <h1>MTK 联发科</h1>
    <p>支持 BROM 模式和 Preloader 模式。</p>
    
    <h2>进入 BROM 模式</h2>
    <ol>
      <li>完全关机</li>
      <li>按住音量-</li>
      <li>插入 USB 数据线</li>
    </ol>
    
    <h2>支持的芯片</h2>
    <p>MT6765, MT6768, MT6785, MT6833, MT6853, MT6873, MT6877, MT6885, MT6893 等</p>
  `,spd:`
    <h1>展锐 Spreadtrum</h1>
    <p>支持研发模式和下载模式。</p>
    
    <h2>进入下载模式</h2>
    <ol>
      <li>完全关机</li>
      <li>按住音量-</li>
      <li>插入 USB 数据线</li>
    </ol>
  `,fastboot:`
    <h1>Fastboot 模式</h1>
    <p>通用的 Android 刷机模式，支持线刷包和 Payload 提取。</p>
    
    <h2>进入 Fastboot</h2>
    <p>关机后按住音量-和电源键，或使用 ADB：</p>
    <pre><code>adb reboot bootloader</code></pre>
    
    <h2>线刷包支持</h2>
    <p>支持自动解析线刷包中的 flash_all.bat，一键刷入所有分区。</p>
  `,"cloud-loader":`
    <h1>云端 Loader</h1>
    <p>自动匹配设备对应的 Loader，无需手动选择。</p>
    
    <h2>工作原理</h2>
    <ol>
      <li>读取设备的 MSM ID 和 PK Hash</li>
      <li>向云端服务器查询匹配的 Loader</li>
      <li>自动下载并使用</li>
    </ol>
    
    <h2>支持的设备</h2>
    <p>云端已收录主流品牌的常见机型 Loader。</p>
  `,"auto-auth":`
    <h1>自动认证</h1>
    <p>自动执行品牌厂商的验证流程。</p>
    
    <h2>小米设备</h2>
    <p>自动执行 MiAuth 认证，无需手动操作。</p>
    
    <h2>一加/OPPO 设备</h2>
    <p>勾选对应的验证选项，软件会自动处理。</p>
  `,partitions:`
    <h1>分区操作</h1>
    <p>支持读取、写入、擦除分区操作。</p>
    
    <h2>读取分区</h2>
    <p>将设备分区数据备份到本地文件。</p>
    
    <h2>写入分区</h2>
    <p>将本地镜像文件刷入设备分区。</p>
    
    <h2>擦除分区</h2>
    <p>清空指定分区的数据。</p>
  `},P=d(()=>{const n=r.params.page||"getting-started";return o[n]||"<h1>页面不存在</h1><p>请从侧边栏选择页面。</p>"});return(n,h)=>{const m=T("router-link");return e(),a("div",V,[f(A,{items:p}),t("main",F,[t("article",{class:"content-body",innerHTML:P.value},null,8,q),s.value||i.value?(e(),a("div",w,[s.value?(e(),M(m,{key:0,to:s.value.link,class:"nav-prev"},{default:_(()=>[h[0]||(h[0]=t("span",{class:"nav-label"},"上一页",-1)),t("span",C,c(s.value.text),1)]),_:1},8,["to"])):g("",!0),i.value?(e(),M(m,{key:1,to:i.value.link,class:"nav-next"},{default:_(()=>[h[1]||(h[1]=t("span",{class:"nav-label"},"下一页",-1)),t("span",N,c(i.value.text),1)]),_:1},8,["to"])):g("",!0)])):g("",!0)])])}}},H=b(U,[["__scopeId","data-v-c645a532"]]);export{H as default};
