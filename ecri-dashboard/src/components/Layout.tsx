import { useState } from 'react'
import { Outlet, useNavigate, useLocation } from 'react-router-dom'
import {
  Box,
  Drawer,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Typography,
  Avatar,
  IconButton,
  Tooltip,
  Badge,
  Divider,
  Snackbar,
  Alert as MuiAlert,
} from '@mui/material'
import {
  GridView as DashboardIcon,
  Shield as ShieldIcon,
  Description as DocumentIcon,
  Business as BusinessIcon,
  History as HistoryIcon,
  Notifications as NotificationsIcon,
  ChevronLeft as ChevronLeftIcon,
  ChevronRight as ChevronRightIcon,
  Logout as LogoutIcon,
  Security as SecurityIcon,
} from '@mui/icons-material'
import { alpha } from '@mui/material/styles'
import { useQuery } from '@tanstack/react-query'
import { useAuthStore } from '../store/auth'
import { alertsApi } from '../api/endpoints'

const DRAWER_WIDTH = 240
const DRAWER_COLLAPSED = 64

const NAV_ITEMS = [
  { label: 'Dashboard', path: '/', icon: DashboardIcon },
  { label: 'Compliance', path: '/compliance', icon: ShieldIcon },
  { label: 'Documents', path: '/documents', icon: DocumentIcon },
  { label: 'Vendors', path: '/vendors', icon: BusinessIcon },
  { label: 'Audit Log', path: '/audit', icon: HistoryIcon },
  { label: 'Alerts', path: '/alerts', icon: NotificationsIcon, badge: true },
]

export default function Layout() {
  const [collapsed, setCollapsed] = useState(false)
  const [snackbar, setSnackbar] = useState<{ open: boolean; message: string; severity: 'success' | 'error' }>({
    open: false, message: '', severity: 'success',
  })

  const navigate = useNavigate()
  const location = useLocation()
  const { user, logout } = useAuthStore()

  const { data: alerts } = useQuery({
    queryKey: ['alerts-active', user?.enterpriseId],
    queryFn: () => alertsApi.getActive(user!.enterpriseId),
    enabled: !!user?.enterpriseId,
    refetchInterval: 30_000,
    retry: false,
  })

  const activeAlertCount = alerts?.filter((a) => !a.isAcknowledged).length ?? 0

  function handleLogout() {
    logout()
    navigate('/login')
  }

  const drawerWidth = collapsed ? DRAWER_COLLAPSED : DRAWER_WIDTH

  return (
    <Box sx={{ display: 'flex', height: '100vh', overflow: 'hidden' }}>
      {/* Sidebar */}
      <Drawer
        variant="permanent"
        sx={{
          width: drawerWidth,
          flexShrink: 0,
          transition: 'width 0.2s ease',
          '& .MuiDrawer-paper': {
            width: drawerWidth,
            transition: 'width 0.2s ease',
            overflow: 'hidden',
            backgroundColor: '#161B22',
            borderRight: '1px solid #30363D',
            display: 'flex',
            flexDirection: 'column',
          },
        }}
      >
        {/* Logo area */}
        <Box
          sx={{
            px: collapsed ? 1 : 2,
            py: 2,
            display: 'flex',
            alignItems: 'center',
            gap: 1.5,
            minHeight: 64,
            borderBottom: '1px solid #30363D',
          }}
        >
          <Box
            sx={{
              width: 36,
              height: 36,
              borderRadius: 1.5,
              background: 'linear-gradient(135deg, #2F81F7 0%, #1F6FEB 100%)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <SecurityIcon sx={{ fontSize: 20, color: '#fff' }} />
          </Box>
          {!collapsed && (
            <Box>
              <Typography
                variant="subtitle1"
                sx={{ fontWeight: 700, fontSize: '0.95rem', lineHeight: 1.1, color: 'text.primary' }}
              >
                ECRI
              </Typography>
              <Typography
                variant="caption"
                sx={{ color: 'text.secondary', fontSize: '0.65rem', letterSpacing: '0.04em' }}
              >
                COMPLIANCE & RISK
              </Typography>
            </Box>
          )}
        </Box>

        {/* Navigation */}
        <List sx={{ px: 1, py: 1.5, flex: 1 }}>
          {NAV_ITEMS.map((item) => {
            const Icon = item.icon
            const isActive =
              item.path === '/'
                ? location.pathname === '/'
                : location.pathname.startsWith(item.path)

            return (
              <ListItem key={item.path} disablePadding sx={{ mb: 0.25 }}>
                <Tooltip title={collapsed ? item.label : ''} placement="right">
                  <ListItemButton
                    onClick={() => navigate(item.path)}
                    sx={{
                      borderRadius: 1.5,
                      px: collapsed ? 1.5 : 2,
                      py: 1,
                      minHeight: 40,
                      backgroundColor: isActive ? alpha('#2F81F7', 0.15) : 'transparent',
                      color: isActive ? '#58A6FF' : '#8B949E',
                      '&:hover': {
                        backgroundColor: isActive ? alpha('#2F81F7', 0.2) : alpha('#ffffff', 0.04),
                        color: isActive ? '#58A6FF' : '#E6EDF3',
                      },
                    }}
                  >
                    <ListItemIcon
                      sx={{
                        minWidth: collapsed ? 0 : 32,
                        color: 'inherit',
                        justifyContent: collapsed ? 'center' : 'flex-start',
                      }}
                    >
                      {item.badge && activeAlertCount > 0 ? (
                        <Badge
                          badgeContent={activeAlertCount}
                          color="error"
                          max={99}
                          sx={{
                            '& .MuiBadge-badge': {
                              fontSize: '0.6rem',
                              height: 16,
                              minWidth: 16,
                            },
                          }}
                        >
                          <Icon sx={{ fontSize: 20 }} />
                        </Badge>
                      ) : (
                        <Icon sx={{ fontSize: 20 }} />
                      )}
                    </ListItemIcon>
                    {!collapsed && (
                      <ListItemText
                        primary={item.label}
                        primaryTypographyProps={{
                          fontSize: '0.875rem',
                          fontWeight: isActive ? 600 : 400,
                        }}
                      />
                    )}
                    {!collapsed && item.badge && activeAlertCount > 0 && (
                      <Box
                        sx={{
                          px: 0.75,
                          py: 0.125,
                          borderRadius: 10,
                          backgroundColor: '#F85149',
                          minWidth: 20,
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                        }}
                      >
                        <Typography sx={{ color: '#fff', fontSize: '0.65rem', fontWeight: 700 }}>
                          {activeAlertCount}
                        </Typography>
                      </Box>
                    )}
                  </ListItemButton>
                </Tooltip>
              </ListItem>
            )
          })}
        </List>

        <Divider />

        {/* Collapse toggle */}
        <Box sx={{ px: 1, py: 1 }}>
          <Tooltip title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'} placement="right">
            <IconButton
              onClick={() => setCollapsed(!collapsed)}
              size="small"
              sx={{
                width: '100%',
                borderRadius: 1.5,
                color: 'text.secondary',
                '&:hover': { backgroundColor: alpha('#ffffff', 0.04), color: 'text.primary' },
              }}
            >
              {collapsed ? <ChevronRightIcon fontSize="small" /> : <ChevronLeftIcon fontSize="small" />}
            </IconButton>
          </Tooltip>
        </Box>

        <Divider />

        {/* User info */}
        <Box sx={{ px: 1.5, py: 2 }}>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1.5,
              justifyContent: collapsed ? 'center' : 'flex-start',
            }}
          >
            <Avatar
              sx={{
                width: 32,
                height: 32,
                fontSize: '0.8rem',
                fontWeight: 600,
                backgroundColor: alpha('#2F81F7', 0.2),
                color: '#2F81F7',
                flexShrink: 0,
              }}
            >
              {user?.email?.[0]?.toUpperCase() ?? 'U'}
            </Avatar>
            {!collapsed && (
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography
                  variant="body2"
                  sx={{
                    fontWeight: 500,
                    fontSize: '0.8rem',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    whiteSpace: 'nowrap',
                    color: 'text.primary',
                  }}
                >
                  {user?.email}
                </Typography>
                <Typography variant="caption" sx={{ color: 'text.secondary', fontSize: '0.7rem' }}>
                  {user?.role}
                </Typography>
              </Box>
            )}
            <Tooltip title="Sign out" placement={collapsed ? 'right' : 'top'}>
              <IconButton
                size="small"
                onClick={handleLogout}
                sx={{ color: 'text.secondary', '&:hover': { color: '#F85149' } }}
              >
                <LogoutIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          </Box>
        </Box>
      </Drawer>

      {/* Main content */}
      <Box
        component="main"
        sx={{
          flex: 1,
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
          backgroundColor: 'background.default',
        }}
      >
        <Box sx={{ flex: 1, overflow: 'auto', p: 3 }}>
          <Outlet context={{ setSnackbar }} />
        </Box>
      </Box>

      <Snackbar
        open={snackbar.open}
        autoHideDuration={4000}
        onClose={() => setSnackbar((s) => ({ ...s, open: false }))}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
      >
        <MuiAlert
          onClose={() => setSnackbar((s) => ({ ...s, open: false }))}
          severity={snackbar.severity}
          variant="filled"
          sx={{ border: '1px solid', borderColor: snackbar.severity === 'error' ? '#F85149' : '#3FB950' }}
        >
          {snackbar.message}
        </MuiAlert>
      </Snackbar>
    </Box>
  )
}
