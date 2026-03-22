import { createTheme, alpha } from '@mui/material/styles'

const theme = createTheme({
  palette: {
    mode: 'dark',
    background: {
      default: '#0D1117',
      paper: '#161B22',
    },
    primary: {
      main: '#2F81F7',
      light: '#58A6FF',
      dark: '#1F6FEB',
    },
    secondary: {
      main: '#8B949E',
    },
    success: {
      main: '#3FB950',
      light: '#56D364',
      dark: '#2EA043',
    },
    warning: {
      main: '#D29922',
      light: '#E3B341',
      dark: '#BB8009',
    },
    error: {
      main: '#F85149',
      light: '#FF7B72',
      dark: '#DA3633',
    },
    text: {
      primary: '#E6EDF3',
      secondary: '#8B949E',
    },
    divider: '#30363D',
  },
  typography: {
    fontFamily: '"Inter", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
    h1: { fontWeight: 700, letterSpacing: '-0.02em' },
    h2: { fontWeight: 700, letterSpacing: '-0.01em' },
    h3: { fontWeight: 600, letterSpacing: '-0.01em' },
    h4: { fontWeight: 600 },
    h5: { fontWeight: 600 },
    h6: { fontWeight: 600 },
    subtitle1: { fontWeight: 500 },
    subtitle2: { fontWeight: 500, color: '#8B949E' },
    button: { fontWeight: 600, textTransform: 'none', letterSpacing: '0' },
  },
  shape: {
    borderRadius: 8,
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          scrollbarWidth: 'thin',
          scrollbarColor: '#30363D #0D1117',
          '&::-webkit-scrollbar': { width: 6 },
          '&::-webkit-scrollbar-track': { background: '#0D1117' },
          '&::-webkit-scrollbar-thumb': { background: '#30363D', borderRadius: 3 },
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
          borderRadius: 8,
          border: '1px solid #30363D',
        },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
          border: '1px solid #30363D',
        },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: {
          borderRadius: 8,
          padding: '7px 16px',
          fontSize: '0.875rem',
        },
        contained: {
          boxShadow: 'none',
          '&:hover': { boxShadow: 'none' },
        },
        outlined: {
          borderColor: '#30363D',
          '&:hover': { borderColor: '#58A6FF', background: alpha('#2F81F7', 0.08) },
        },
      },
    },
    MuiTextField: {
      defaultProps: { variant: 'outlined', size: 'small' },
      styleOverrides: {
        root: {
          '& .MuiOutlinedInput-root': {
            backgroundColor: '#0D1117',
            '& fieldset': { borderColor: '#30363D' },
            '&:hover fieldset': { borderColor: '#58A6FF' },
            '&.Mui-focused fieldset': { borderColor: '#2F81F7' },
          },
        },
      },
    },
    MuiSelect: {
      styleOverrides: {
        root: {
          backgroundColor: '#0D1117',
        },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        root: {
          borderBottom: '1px solid #30363D',
        },
        head: {
          backgroundColor: '#161B22',
          fontWeight: 600,
          color: '#8B949E',
          fontSize: '0.75rem',
          textTransform: 'uppercase',
          letterSpacing: '0.05em',
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          borderRadius: 4,
          fontWeight: 500,
          fontSize: '0.75rem',
        },
      },
    },
    MuiDialog: {
      styleOverrides: {
        paper: {
          background: '#161B22',
          border: '1px solid #30363D',
        },
      },
    },
    MuiTooltip: {
      styleOverrides: {
        tooltip: {
          backgroundColor: '#21262D',
          border: '1px solid #30363D',
          fontSize: '0.75rem',
        },
      },
    },
    MuiLinearProgress: {
      styleOverrides: {
        root: {
          borderRadius: 4,
          backgroundColor: '#21262D',
        },
      },
    },
    MuiTabs: {
      styleOverrides: {
        root: {
          borderBottom: '1px solid #30363D',
        },
        indicator: {
          backgroundColor: '#2F81F7',
        },
      },
    },
    MuiTab: {
      styleOverrides: {
        root: {
          textTransform: 'none',
          fontWeight: 500,
          fontSize: '0.875rem',
          color: '#8B949E',
          '&.Mui-selected': { color: '#E6EDF3' },
        },
      },
    },
    MuiDivider: {
      styleOverrides: {
        root: {
          borderColor: '#30363D',
        },
      },
    },
    MuiDrawer: {
      styleOverrides: {
        paper: {
          border: 'none',
          borderLeft: '1px solid #30363D',
        },
      },
    },
    MuiDataGrid: {
      styleOverrides: {
        root: {
          border: 'none',
          '--DataGrid-rowBorderColor': '#21262D',
          '& .MuiDataGrid-columnHeaders': {
            backgroundColor: '#0D1117',
            borderBottom: '1px solid #30363D',
          },
          '& .MuiDataGrid-columnHeaderTitle': {
            fontWeight: 600,
            fontSize: '0.72rem',
            textTransform: 'uppercase',
            letterSpacing: '0.05em',
            color: '#8B949E',
          },
          '& .MuiDataGrid-row': {
            borderBottom: '1px solid #21262D',
            '&:hover': { backgroundColor: 'rgba(255,255,255,0.02)' },
            '&.Mui-selected': { backgroundColor: 'rgba(47,129,247,0.06)' },
          },
          '& .MuiDataGrid-cell': {
            borderBottom: 'none',
            fontSize: '0.875rem',
          },
          '& .MuiDataGrid-footerContainer': {
            borderTop: '1px solid #30363D',
          },
          '& .MuiDataGrid-virtualScroller': {
            backgroundColor: 'transparent',
          },
          '& .MuiTablePagination-root': {
            color: '#8B949E',
            fontSize: '0.8rem',
          },
        },
      },
    },
    MuiInputLabel: {
      styleOverrides: {
        root: {
          fontSize: '0.875rem',
          color: '#8B949E',
          '&.Mui-focused': { color: '#2F81F7' },
        },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          fontSize: '0.875rem',
        },
      },
    },
    MuiListItemButton: {
      styleOverrides: {
        root: {
          borderRadius: 8,
          transition: 'background-color 0.15s ease, color 0.15s ease',
        },
      },
    },
    MuiSkeleton: {
      styleOverrides: {
        root: {
          backgroundColor: '#21262D',
          '&::after': {
            background: 'linear-gradient(90deg, transparent, rgba(255,255,255,0.04), transparent)',
          },
        },
      },
    },
  },
})

export default theme

// Severity colors used throughout the app
export const SEVERITY_COLORS = {
  Critical: '#EF4444',
  High: '#F97316',
  Medium: '#EAB308',
  Low: '#22C55E',
  1: '#22C55E',
  2: '#EAB308',
  3: '#F97316',
  4: '#EF4444',
} as const

export const RISK_LEVEL_COLORS = {
  Negligible: '#6B7280',
  Low: '#22C55E',
  Medium: '#EAB308',
  High: '#F97316',
  Critical: '#EF4444',
  0: '#6B7280',
  1: '#22C55E',
  2: '#EAB308',
  3: '#F97316',
  4: '#EF4444',
} as const
