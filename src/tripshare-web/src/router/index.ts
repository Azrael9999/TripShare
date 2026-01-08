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
import Messages from '../views/Messages.vue'
import SignIn from '../views/SignIn.vue'
import ResetPassword from '../views/ResetPassword.vue'
import AdminDashboard from '../views/admin/AdminDashboard.vue'
import AdminReports from '../views/admin/AdminReports.vue'
import AdminIdentity from '../views/admin/AdminIdentity.vue'
import AdminAds from '../views/admin/AdminAds.vue'
import { useAuthStore } from '../stores/auth'
import { useAdsStore } from '../stores/ads'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: Home, meta: { title: 'HopTrip Sri Lanka | Carpool & Ride Sharing' } },
    { path: '/trips/:id', component: TripDetails, meta: { title: 'Trip details | HopTrip Sri Lanka' } },
    { path: '/create', component: CreateTrip, meta: { requiresAuth: true, requiresVerified: true, title: 'Create trip | HopTrip' } },
    { path: '/my-trips', component: MyTrips, meta: { requiresAuth: true, requiresVerified: true, title: 'My trips | HopTrip' } },
    { path: '/bookings', component: MyBookings, meta: { requiresAuth: true, requiresVerified: true, title: 'My bookings | HopTrip' } },
    { path: '/notifications', component: Notifications, meta: { requiresAuth: true, title: 'Alerts | HopTrip' } },
    { path: '/messages', component: Messages, meta: { requiresAuth: true, title: 'Messages | HopTrip' } },
    { path: '/profile', component: Profile, meta: { requiresAuth: true, title: 'Profile | HopTrip' } },
    { path: '/sign-in', component: SignIn, meta: { title: 'Sign in | HopTrip' } },
    { path: '/reset-password', component: ResetPassword, meta: { title: 'Reset password | HopTrip' } },
    { path: '/vehicle', component: Vehicle, meta: { requiresAuth: true, requiresVerified: true, title: 'Vehicle profile | HopTrip' } },
    { path: '/safety', component: Safety, meta: { requiresAuth: true, title: 'Safety | HopTrip' } },
    { path: '/verify-email', component: VerifyEmail, meta: { requiresAuth: true, title: 'Verify email | HopTrip' } },
    { path: '/admin', component: AdminDashboard, meta: { requiresAuth: true, requiresAdmin: true, title: 'Admin | HopTrip' } },
    { path: '/admin/reports', component: AdminReports, meta: { requiresAuth: true, requiresAdmin: true, title: 'Admin reports | HopTrip' } },
    { path: '/admin/identity', component: AdminIdentity, meta: { requiresAuth: true, requiresAdmin: true, title: 'Admin identity | HopTrip' } },
    { path: '/admin/ads', component: AdminAds, meta: { requiresAuth: true, requiresAdmin: true, title: 'Admin ads | HopTrip' } }
  ]
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  if (!auth.initialized) await auth.init()

  if ((to.meta as any)?.requiresAuth && !auth.isAuthenticated) {
    return { path: '/sign-in' }
  }

  if (to.path === '/sign-in' && auth.isAuthenticated) {
    return { path: '/' }
  }

  if ((to.meta as any)?.requiresVerified && auth.isAuthenticated && (!auth.me?.emailVerified || !auth.me?.phoneVerified)) {
    return { path: '/profile', query: { verify: '1' } }
  }

  if ((to.meta as any)?.requiresAdmin && auth.isAuthenticated && auth.me?.role !== 'admin') {
    return { path: '/' }
  }
})

router.afterEach((to) => {
  const ads = useAdsStore()
  const pageKey = (to.name?.toString() || to.path) ?? 'global'
  ads.resetPageImpressions(pageKey)
})

export default router
