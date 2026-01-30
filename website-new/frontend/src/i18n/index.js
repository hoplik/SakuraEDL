import { ref, computed } from 'vue'

// 支持的语言
export const languages = [
  { code: 'zh', name: '简体中文', flag: '🇨🇳' },
  { code: 'en', name: 'English', flag: '🇺🇸' },
  { code: 'ja', name: '日本語', flag: '🇯🇵' },
  { code: 'ko', name: '한국어', flag: '🇰🇷' },
  { code: 'ru', name: 'Русский', flag: '🇷🇺' },
  { code: 'es', name: 'Español', flag: '🇪🇸' }
]

// 翻译数据
const messages = {
  zh: {
    nav: {
      home: '首页',
      quickStart: '快速开始',
      tutorials: '使用教程',
      qualcomm: '高通 EDL',
      mtk: 'MTK 联发科',
      spd: '展锐 Spreadtrum',
      fastboot: 'Fastboot',
      download: '下载',
      chipDatabase: '芯片数据库',
      qualcommChips: '📱 高通 Qualcomm',
      mtkChips: '⚡ MTK 联发科',
      spdChips: '🔧 展锐 Spreadtrum',
      api: 'API',
      stats: '统计',
      qqGroup: 'QQ群',
      telegram: 'Telegram'
    },
    home: {
      hero: {
        title: 'SakuraEDL',
        subtitle: '多平台手机刷机工具',
        description: '支持高通 EDL / MTK / 展锐 / Fastboot | 永久免费',
        getStarted: '快速开始',
        download: '下载工具',
        viewOnGithub: '在 GitHub 上查看'
      },
      features: {
        qualcomm: {
          title: '高通 EDL 模式',
          desc: '支持 Sahara + Firehose 协议，云端 Loader 自动匹配，支持小米/一加/OPPO 等品牌认证'
        },
        mtk: {
          title: 'MTK 联发科',
          desc: '支持 BROM + DA 模式，XFlash 二进制协议，兼容 MT6765-MT6893 全系芯片'
        },
        spd: {
          title: '展锐 Spreadtrum',
          desc: '支持 BSL + FDL 协议，自动检测芯片型号，兼容 SC9863A/T760 等芯片'
        },
        fastboot: {
          title: 'Fastboot 模式',
          desc: '支持标准 Fastboot 协议，AB 分区自动识别，Payload 刷机支持'
        },
        cloud: {
          title: '云端 Loader',
          desc: '自动匹配设备对应的 Loader，VIP/小米/一加认证自动执行'
        },
        free: {
          title: '完全免费',
          desc: '永久免费使用，无需注册，无广告，开源透明'
        }
      },
      chips: {
        title: '支持的芯片',
        flagship: '旗舰',
        midRange: '中端',
        entry: '入门'
      },
      quickLinks: '快速链接'
    },
    download: {
      title: '下载 SakuraEDL',
      version: '最新版本',
      windows: 'Windows 版本',
      portable: '便携版',
      installer: '安装版',
      requirements: '系统要求',
      requirementsList: [
        'Windows 10/11 (64位)',
        '.NET Framework 4.8',
        'USB 驱动程序'
      ],
      drivers: '驱动下载',
      qualcommDriver: '高通驱动',
      mtkDriver: 'MTK驱动',
      spdDriver: '展锐驱动'
    },
    footer: {
      license: 'MIT License | 永久免费',
      copyright: '© 2025-2026 SakuraEDL'
    }
  },
  en: {
    nav: {
      home: 'Home',
      quickStart: 'Quick Start',
      tutorials: 'Tutorials',
      qualcomm: 'Qualcomm EDL',
      mtk: 'MediaTek MTK',
      spd: 'Spreadtrum SPD',
      fastboot: 'Fastboot',
      download: 'Download',
      chipDatabase: 'Chip Database',
      qualcommChips: '📱 Qualcomm',
      mtkChips: '⚡ MediaTek',
      spdChips: '🔧 Spreadtrum',
      api: 'API',
      stats: 'Statistics',
      qqGroup: 'QQ Group',
      telegram: 'Telegram'
    },
    home: {
      hero: {
        title: 'SakuraEDL',
        subtitle: 'Multi-Platform Mobile Flash Tool',
        description: 'Support Qualcomm EDL / MTK / Spreadtrum / Fastboot | Free Forever',
        getStarted: 'Get Started',
        download: 'Download',
        viewOnGithub: 'View on GitHub'
      },
      features: {
        qualcomm: {
          title: 'Qualcomm EDL Mode',
          desc: 'Sahara + Firehose protocol, cloud Loader auto-match, supports Xiaomi/OnePlus/OPPO authentication'
        },
        mtk: {
          title: 'MediaTek MTK',
          desc: 'BROM + DA mode, XFlash binary protocol, compatible with MT6765-MT6893 series'
        },
        spd: {
          title: 'Spreadtrum SPD',
          desc: 'BSL + FDL protocol, auto chip detection, compatible with SC9863A/T760'
        },
        fastboot: {
          title: 'Fastboot Mode',
          desc: 'Standard Fastboot protocol, A/B partition auto-detection, Payload flash support'
        },
        cloud: {
          title: 'Cloud Loader',
          desc: 'Auto-match device Loader, VIP/Xiaomi/OnePlus auth auto-execute'
        },
        free: {
          title: 'Completely Free',
          desc: 'Free forever, no registration, no ads, open source'
        }
      },
      chips: {
        title: 'Supported Chips',
        flagship: 'Flagship',
        midRange: 'Mid-Range',
        entry: 'Entry'
      },
      quickLinks: 'Quick Links'
    },
    download: {
      title: 'Download SakuraEDL',
      version: 'Latest Version',
      windows: 'Windows Version',
      portable: 'Portable',
      installer: 'Installer',
      requirements: 'System Requirements',
      requirementsList: [
        'Windows 10/11 (64-bit)',
        '.NET Framework 4.8',
        'USB Drivers'
      ],
      drivers: 'Driver Downloads',
      qualcommDriver: 'Qualcomm Driver',
      mtkDriver: 'MTK Driver',
      spdDriver: 'Spreadtrum Driver'
    },
    footer: {
      license: 'MIT License | Free Forever',
      copyright: '© 2025-2026 SakuraEDL'
    }
  },
  ja: {
    nav: {
      home: 'ホーム',
      quickStart: 'クイックスタート',
      tutorials: 'チュートリアル',
      qualcomm: 'Qualcomm EDL',
      mtk: 'MediaTek MTK',
      spd: 'Spreadtrum SPD',
      fastboot: 'Fastboot',
      download: 'ダウンロード',
      chipDatabase: 'チップDB',
      qualcommChips: '📱 Qualcomm',
      mtkChips: '⚡ MediaTek',
      spdChips: '🔧 Spreadtrum',
      api: 'API',
      stats: '統計',
      qqGroup: 'QQグループ',
      telegram: 'Telegram'
    },
    home: {
      hero: {
        title: 'SakuraEDL',
        subtitle: 'マルチプラットフォームモバイルフラッシュツール',
        description: 'Qualcomm EDL / MTK / Spreadtrum / Fastboot 対応 | 永久無料',
        getStarted: '始める',
        download: 'ダウンロード',
        viewOnGithub: 'GitHubで見る'
      },
      features: {
        qualcomm: {
          title: 'Qualcomm EDL モード',
          desc: 'Sahara + Firehose プロトコル、クラウドLoader自動マッチング、Xiaomi/OnePlus/OPPO認証対応'
        },
        mtk: {
          title: 'MediaTek MTK',
          desc: 'BROM + DA モード、XFlashバイナリプロトコル、MT6765-MT6893シリーズ対応'
        },
        spd: {
          title: 'Spreadtrum SPD',
          desc: 'BSL + FDL プロトコル、チップ自動検出、SC9863A/T760対応'
        },
        fastboot: {
          title: 'Fastboot モード',
          desc: '標準Fastbootプロトコル、A/Bパーティション自動識別、Payloadフラッシュ対応'
        },
        cloud: {
          title: 'クラウドLoader',
          desc: 'デバイスLoader自動マッチング、VIP/Xiaomi/OnePlus認証自動実行'
        },
        free: {
          title: '完全無料',
          desc: '永久無料、登録不要、広告なし、オープンソース'
        }
      },
      chips: {
        title: '対応チップ',
        flagship: 'フラッグシップ',
        midRange: 'ミッドレンジ',
        entry: 'エントリー'
      },
      quickLinks: 'クイックリンク'
    },
    download: {
      title: 'SakuraEDLをダウンロード',
      version: '最新バージョン',
      windows: 'Windows版',
      portable: 'ポータブル版',
      installer: 'インストーラー版',
      requirements: 'システム要件',
      requirementsList: [
        'Windows 10/11 (64ビット)',
        '.NET Framework 4.8',
        'USBドライバ'
      ],
      drivers: 'ドライバダウンロード',
      qualcommDriver: 'Qualcommドライバ',
      mtkDriver: 'MTKドライバ',
      spdDriver: 'Spreadtrumドライバ'
    },
    footer: {
      license: 'MIT License | 永久無料',
      copyright: '© 2025-2026 SakuraEDL'
    }
  },
  ko: {
    nav: {
      home: '홈',
      quickStart: '빠른 시작',
      tutorials: '튜토리얼',
      qualcomm: 'Qualcomm EDL',
      mtk: 'MediaTek MTK',
      spd: 'Spreadtrum SPD',
      fastboot: 'Fastboot',
      download: '다운로드',
      chipDatabase: '칩 DB',
      qualcommChips: '📱 Qualcomm',
      mtkChips: '⚡ MediaTek',
      spdChips: '🔧 Spreadtrum',
      api: 'API',
      stats: '통계',
      qqGroup: 'QQ 그룹',
      telegram: 'Telegram'
    },
    home: {
      hero: {
        title: 'SakuraEDL',
        subtitle: '멀티 플랫폼 모바일 플래시 도구',
        description: 'Qualcomm EDL / MTK / Spreadtrum / Fastboot 지원 | 영구 무료',
        getStarted: '시작하기',
        download: '다운로드',
        viewOnGithub: 'GitHub에서 보기'
      },
      features: {
        qualcomm: {
          title: 'Qualcomm EDL 모드',
          desc: 'Sahara + Firehose 프로토콜, 클라우드 Loader 자동 매칭, Xiaomi/OnePlus/OPPO 인증 지원'
        },
        mtk: {
          title: 'MediaTek MTK',
          desc: 'BROM + DA 모드, XFlash 바이너리 프로토콜, MT6765-MT6893 시리즈 호환'
        },
        spd: {
          title: 'Spreadtrum SPD',
          desc: 'BSL + FDL 프로토콜, 칩 자동 감지, SC9863A/T760 호환'
        },
        fastboot: {
          title: 'Fastboot 모드',
          desc: '표준 Fastboot 프로토콜, A/B 파티션 자동 식별, Payload 플래시 지원'
        },
        cloud: {
          title: '클라우드 Loader',
          desc: '기기 Loader 자동 매칭, VIP/Xiaomi/OnePlus 인증 자동 실행'
        },
        free: {
          title: '완전 무료',
          desc: '영구 무료, 등록 불필요, 광고 없음, 오픈 소스'
        }
      },
      chips: {
        title: '지원 칩',
        flagship: '플래그십',
        midRange: '미드레인지',
        entry: '엔트리'
      },
      quickLinks: '빠른 링크'
    },
    download: {
      title: 'SakuraEDL 다운로드',
      version: '최신 버전',
      windows: 'Windows 버전',
      portable: '포터블',
      installer: '설치 프로그램',
      requirements: '시스템 요구 사항',
      requirementsList: [
        'Windows 10/11 (64비트)',
        '.NET Framework 4.8',
        'USB 드라이버'
      ],
      drivers: '드라이버 다운로드',
      qualcommDriver: 'Qualcomm 드라이버',
      mtkDriver: 'MTK 드라이버',
      spdDriver: 'Spreadtrum 드라이버'
    },
    footer: {
      license: 'MIT License | 영구 무료',
      copyright: '© 2025-2026 SakuraEDL'
    }
  },
  ru: {
    nav: {
      home: 'Главная',
      quickStart: 'Быстрый старт',
      tutorials: 'Руководства',
      qualcomm: 'Qualcomm EDL',
      mtk: 'MediaTek MTK',
      spd: 'Spreadtrum SPD',
      fastboot: 'Fastboot',
      download: 'Скачать',
      chipDatabase: 'База чипов',
      qualcommChips: '📱 Qualcomm',
      mtkChips: '⚡ MediaTek',
      spdChips: '🔧 Spreadtrum',
      api: 'API',
      stats: 'Статистика',
      qqGroup: 'QQ группа',
      telegram: 'Telegram'
    },
    home: {
      hero: {
        title: 'SakuraEDL',
        subtitle: 'Мультиплатформенный инструмент прошивки',
        description: 'Поддержка Qualcomm EDL / MTK / Spreadtrum / Fastboot | Бесплатно навсегда',
        getStarted: 'Начать',
        download: 'Скачать',
        viewOnGithub: 'Смотреть на GitHub'
      },
      features: {
        qualcomm: {
          title: 'Qualcomm EDL режим',
          desc: 'Протоколы Sahara + Firehose, облачный Loader, поддержка Xiaomi/OnePlus/OPPO'
        },
        mtk: {
          title: 'MediaTek MTK',
          desc: 'Режим BROM + DA, протокол XFlash, совместим с MT6765-MT6893'
        },
        spd: {
          title: 'Spreadtrum SPD',
          desc: 'Протокол BSL + FDL, автоопределение чипа, совместим с SC9863A/T760'
        },
        fastboot: {
          title: 'Режим Fastboot',
          desc: 'Стандартный Fastboot, автоопределение A/B разделов, поддержка Payload'
        },
        cloud: {
          title: 'Облачный Loader',
          desc: 'Автоподбор Loader, автоматическая VIP/Xiaomi/OnePlus авторизация'
        },
        free: {
          title: 'Полностью бесплатно',
          desc: 'Бесплатно навсегда, без регистрации, без рекламы, открытый исходный код'
        }
      },
      chips: {
        title: 'Поддерживаемые чипы',
        flagship: 'Флагман',
        midRange: 'Средний класс',
        entry: 'Начальный уровень'
      },
      quickLinks: 'Быстрые ссылки'
    },
    download: {
      title: 'Скачать SakuraEDL',
      version: 'Последняя версия',
      windows: 'Windows версия',
      portable: 'Портативная',
      installer: 'Установщик',
      requirements: 'Системные требования',
      requirementsList: [
        'Windows 10/11 (64-бит)',
        '.NET Framework 4.8',
        'USB драйверы'
      ],
      drivers: 'Драйверы',
      qualcommDriver: 'Драйвер Qualcomm',
      mtkDriver: 'Драйвер MTK',
      spdDriver: 'Драйвер Spreadtrum'
    },
    footer: {
      license: 'MIT License | Бесплатно навсегда',
      copyright: '© 2025-2026 SakuraEDL'
    }
  },
  es: {
    nav: {
      home: 'Inicio',
      quickStart: 'Inicio rápido',
      tutorials: 'Tutoriales',
      qualcomm: 'Qualcomm EDL',
      mtk: 'MediaTek MTK',
      spd: 'Spreadtrum SPD',
      fastboot: 'Fastboot',
      download: 'Descargar',
      chipDatabase: 'Base de chips',
      qualcommChips: '📱 Qualcomm',
      mtkChips: '⚡ MediaTek',
      spdChips: '🔧 Spreadtrum',
      api: 'API',
      stats: 'Estadísticas',
      qqGroup: 'Grupo QQ',
      telegram: 'Telegram'
    },
    home: {
      hero: {
        title: 'SakuraEDL',
        subtitle: 'Herramienta de flash multiplataforma',
        description: 'Soporta Qualcomm EDL / MTK / Spreadtrum / Fastboot | Gratis para siempre',
        getStarted: 'Comenzar',
        download: 'Descargar',
        viewOnGithub: 'Ver en GitHub'
      },
      features: {
        qualcomm: {
          title: 'Modo Qualcomm EDL',
          desc: 'Protocolos Sahara + Firehose, Loader en la nube, soporta Xiaomi/OnePlus/OPPO'
        },
        mtk: {
          title: 'MediaTek MTK',
          desc: 'Modo BROM + DA, protocolo XFlash binario, compatible con MT6765-MT6893'
        },
        spd: {
          title: 'Spreadtrum SPD',
          desc: 'Protocolo BSL + FDL, detección automática de chip, compatible con SC9863A/T760'
        },
        fastboot: {
          title: 'Modo Fastboot',
          desc: 'Protocolo Fastboot estándar, detección automática A/B, soporte Payload'
        },
        cloud: {
          title: 'Loader en la nube',
          desc: 'Auto-coincidencia de Loader, autenticación VIP/Xiaomi/OnePlus automática'
        },
        free: {
          title: 'Completamente gratis',
          desc: 'Gratis para siempre, sin registro, sin anuncios, código abierto'
        }
      },
      chips: {
        title: 'Chips soportados',
        flagship: 'Flagship',
        midRange: 'Gama media',
        entry: 'Entrada'
      },
      quickLinks: 'Enlaces rápidos'
    },
    download: {
      title: 'Descargar SakuraEDL',
      version: 'Última versión',
      windows: 'Versión Windows',
      portable: 'Portátil',
      installer: 'Instalador',
      requirements: 'Requisitos del sistema',
      requirementsList: [
        'Windows 10/11 (64 bits)',
        '.NET Framework 4.8',
        'Controladores USB'
      ],
      drivers: 'Controladores',
      qualcommDriver: 'Controlador Qualcomm',
      mtkDriver: 'Controlador MTK',
      spdDriver: 'Controlador Spreadtrum'
    },
    footer: {
      license: 'MIT License | Gratis para siempre',
      copyright: '© 2025-2026 SakuraEDL'
    }
  }
}

// 当前语言
const currentLang = ref(localStorage.getItem('lang') || 'zh')

// 设置语言
export function setLanguage(lang) {
  currentLang.value = lang
  localStorage.setItem('lang', lang)
  document.documentElement.lang = lang
}

// 获取当前语言
export function useLanguage() {
  return currentLang
}

// 翻译函数
export function useI18n() {
  const t = (key) => {
    const keys = key.split('.')
    let value = messages[currentLang.value]
    for (const k of keys) {
      value = value?.[k]
    }
    return value || key
  }
  
  return { t, currentLang, setLanguage, languages }
}
