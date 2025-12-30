import { createRouter, createWebHistory } from 'vue-router'
import Home from '../views/Home.vue'
import TripDetails from '../views/TripDetails.vue'
import CreateTrip from '../views/CreateTrip.vue'
import MyBookings from '../views/MyBookings.vue'
import Profile from '../views/Profile.vue'
import VerifyEmail from '../views/VerifyEmail.vue'
import Notifications from '../views/Notifications.vue'
import Vehicle from '../views/Vehicle.vue'
import Safety from '../views/Safety.vue'
import MyTrips from '../views/MyTrips.vue'
import AdminDashboard from '../views/admin/AdminDashboard.vue'
import AdminReports from '../views/admin/AdminReports.vue'
import AdminIdentity from '../views/admin/AdminIdentity.vue'
import AdminAds from '../views/admin/AdminAds.vue'
import { useAuthStore } from '../stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: Home },
    { path: '/trips/:id', component: TripDetails },
    { path: '/create', component: CreateTrip, meta: { requiresAuth: true, requiresVerified: true } },
    { path: '/my-trips', component: MyTrips, meta: { requiresAuth: true, requiresVerified: true } },
    { path: '/bookings', component: MyBookings, meta: { requiresAuth: true, requiresVerified: true } },
    { path: '/notifications', component: Notifications, meta: { requiresAuth: true } },
    { path: '/profile', component: Profile, meta: { requiresAuth: true } },
    { path: '/vehicle', component: Vehicle, meta: { requiresAuth: true, requiresVerified: true } },
    { path: '/safety', component: Safety, meta: { requiresAuth: true } },
    { path: '/verify-email', component: VerifyEmail, meta: { requiresAuth: true } },
    { path: '/admin', component: AdminDashboard, meta: { requiresAuth: true, requiresAdmin: true } },
    { path: '/admin/reports', component: AdminReports, meta: { requiresAuth: true, requiresAdmin: true } },
    { path: '/admin/identity', component: AdminIdentity, meta: { requiresAuth: true, requiresAdmin: true } },
    { path: '/admin/ads', component: AdminAds, meta: { requiresAuth: true, requiresAdmin: true } }
  ]
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  if (!auth.initialized) await auth.init()

  if ((to.meta as any)?.requiresAuth && !auth.isAuthenticated) {
    return { path: '/', query: { login: '1' } }
  }

  if ((to.meta as any)?.requiresVerified && auth.isAuthenticated && !auth.me?.emailVerified) {
    return { path: '/profile', query: { verify: '1' } }
  }

  if ((to.meta as any)?.requiresAdmin && auth.isAuthenticated && auth.me?.role !== 'admin') {
    return { path: '/' }
  }
})

export default router
