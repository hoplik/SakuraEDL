export default {
  title: 'SakuraEDL',
  description: '多平台手机刷机工具 - 支持高通/MTK/展锐/Fastboot',
  lang: 'zh-CN',
  
  head: [
    ['meta', { name: 'keywords', content: 'SakuraEDL,EDL,Qualcomm,MTK,Spreadtrum,刷机,救砖,9008' }],
    ['link', { rel: 'icon', href: '/logo.png' }]
  ],

  themeConfig: {
    logo: '/logo.png',
    siteTitle: 'SakuraEDL',
    
    nav: [
      { text: '首页', link: '/' },
      { text: '快速开始', link: '/guide/getting-started' },
      { text: '使用教程', items: [
        { text: '高通 EDL', link: '/guide/qualcomm' },
        { text: 'MTK 联发科', link: '/guide/mtk' },
        { text: '展锐 Spreadtrum', link: '/guide/spd' },
        { text: 'Fastboot', link: '/guide/fastboot' }
      ]},
      { text: '下载', link: '/download' },
      { text: 'API', link: '/api/' },
      { text: 'QQ群', link: 'https://qm.qq.com/q/z3iVnkm22c' }
    ],

    sidebar: {
      '/guide/': [
        {
          text: '入门指南',
          items: [
            { text: '快速开始', link: '/guide/getting-started' },
            { text: '安装说明', link: '/guide/installation' },
            { text: '常见问题', link: '/guide/faq' }
          ]
        },
        {
          text: '平台教程',
          items: [
            { text: '高通 EDL 模式', link: '/guide/qualcomm' },
            { text: 'MTK 联发科', link: '/guide/mtk' },
            { text: '展锐 Spreadtrum', link: '/guide/spd' },
            { text: 'Fastboot 模式', link: '/guide/fastboot' }
          ]
        },
        {
          text: '进阶功能',
          items: [
            { text: '云端 Loader', link: '/guide/cloud-loader' },
            { text: '自动认证', link: '/guide/auto-auth' },
            { text: '分区操作', link: '/guide/partitions' }
          ]
        }
      ],
      '/api/': [
        {
          text: 'API 文档',
          items: [
            { text: '概述', link: '/api/' },
            { text: 'Loader 接口', link: '/api/loaders' },
            { text: '设备匹配', link: '/api/match' }
          ]
        }
      ]
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/xiriovo/SakuraEDL' }
    ],

    footer: {
      message: '永久免费 | 开源工具',
      copyright: 'Copyright © 2024-2026 SakuraEDL Team'
    },

    search: {
      provider: 'local'
    },

    outline: {
      label: '页面导航',
      level: [2, 3]
    },

    lastUpdated: {
      text: '最后更新',
      formatOptions: {
        dateStyle: 'short',
        timeStyle: 'short'
      }
    }
  }
}
